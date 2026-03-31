using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace Azure.InMemory.ServiceBus.InMemory;

public sealed class InMemoryServiceBusState
{
    private const int DefaultMaxDeliveryCount = 10;

    private readonly ConcurrentDictionary<string, byte> _queues = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _topicSubscriptions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryServiceBusEnvelope>> _pendingMessages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryServiceBusEnvelope>> _completedMessages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryServiceBusDeadLetterOutcome>> _deadLetteredMessages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryServiceBusErroredOutcome>> _erroredMessages =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> QueueNames => _queues.Keys.OrderBy(static name => name).ToArray();

    public IReadOnlyCollection<string> TopicNames => _topicSubscriptions.Keys.OrderBy(static name => name).ToArray();

    public bool QueueExists(string queueName) =>
        _queues.ContainsKey(ValidateEntityName(queueName, nameof(queueName)));

    public bool TopicExists(string topicName) =>
        _topicSubscriptions.ContainsKey(ValidateEntityName(topicName, nameof(topicName)));

    public bool SubscriptionExists(string topicName, string subscriptionName)
    {
        topicName = ValidateEntityName(topicName, nameof(topicName));
        subscriptionName = ValidateEntityName(subscriptionName, nameof(subscriptionName));

        return _topicSubscriptions.TryGetValue(topicName, out var subscriptions)
            && subscriptions.ContainsKey(subscriptionName);
    }

    public IReadOnlyList<InMemoryServiceBusEnvelope> GetPendingMessages(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));

        return _pendingMessages.TryGetValue(entityPath, out var messages)
            ? messages.ToArray()
            : Array.Empty<InMemoryServiceBusEnvelope>();
    }

    public IReadOnlyList<InMemoryServiceBusEnvelope> GetCompletedMessages(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));

        return _completedMessages.TryGetValue(entityPath, out var messages)
            ? messages.ToArray()
            : Array.Empty<InMemoryServiceBusEnvelope>();
    }

    public IReadOnlyList<InMemoryServiceBusDeadLetterOutcome> GetDeadLetteredMessages(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));

        return _deadLetteredMessages.TryGetValue(entityPath, out var messages)
            ? messages.ToArray()
            : Array.Empty<InMemoryServiceBusDeadLetterOutcome>();
    }

    public IReadOnlyList<InMemoryServiceBusErroredOutcome> GetErroredMessages(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));

        return _erroredMessages.TryGetValue(entityPath, out var messages)
            ? messages.ToArray()
            : Array.Empty<InMemoryServiceBusErroredOutcome>();
    }

    public void EnsureQueue(string queueName)
    {
        queueName = ValidateEntityName(queueName, nameof(queueName));
        _queues.TryAdd(queueName, 0);
        EnsureEntityStores(queueName);
    }

    public void EnsureTopic(string topicName)
    {
        topicName = ValidateEntityName(topicName, nameof(topicName));
        _topicSubscriptions.TryAdd(topicName, new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
    }

    public void EnsureSubscription(string topicName, string subscriptionName)
    {
        topicName = ValidateEntityName(topicName, nameof(topicName));
        subscriptionName = ValidateEntityName(subscriptionName, nameof(subscriptionName));

        var subscriptions = _topicSubscriptions.GetOrAdd(
            topicName,
            static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        subscriptions.TryAdd(subscriptionName, 0);
        EnsureEntityStores(GetSubscriptionEntityPath(topicName, subscriptionName));
    }

    internal bool ProcessorEntityExists(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        return _pendingMessages.ContainsKey(entityPath);
    }

    internal IReadOnlyList<InMemoryServiceBusEnvelope> DequeuePendingBatch(string entityPath)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));

        var pending = GetEnvelopeStore(
            _pendingMessages,
            entityPath,
            "pending-message buffer",
            "processing messages");

        var batch = new List<InMemoryServiceBusEnvelope>();
        while (pending.TryDequeue(out var envelope))
        {
            batch.Add(envelope);
        }

        return batch;
    }

    internal static InMemoryServiceBusEnvelope PrepareForProcessing(
        string entityPath,
        InMemoryServiceBusEnvelope envelope,
        int configuredMaxDeliveryCount)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureEnvelopeBelongsToEntity(entityPath, envelope);

        configuredMaxDeliveryCount = ValidateMaxDeliveryCount(
            configuredMaxDeliveryCount,
            nameof(configuredMaxDeliveryCount),
            $"while preparing message '{envelope.MessageId ?? "<no-message-id>"}' for processing on '{entityPath}'");

        ValidateEnvelopeDeliveryMetadata(envelope, entityPath, "preparing it for processing");

        if (envelope.DeliveryCount > 1)
        {
            return envelope;
        }

        return envelope.MaxDeliveryCount == configuredMaxDeliveryCount
            ? envelope
            : envelope with { MaxDeliveryCount = configuredMaxDeliveryCount };
    }

    internal static bool HasReachedMaxDeliveryCount(string entityPath, InMemoryServiceBusEnvelope envelope)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureEnvelopeBelongsToEntity(entityPath, envelope);
        ValidateEnvelopeDeliveryMetadata(envelope, entityPath, "checking max-delivery exhaustion");

        return envelope.DeliveryCount >= envelope.MaxDeliveryCount;
    }

    internal static InMemoryServiceBusEnvelope PrepareForNextDelivery(string entityPath, InMemoryServiceBusEnvelope envelope)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureEnvelopeBelongsToEntity(entityPath, envelope);
        ValidateEnvelopeDeliveryMetadata(envelope, entityPath, "incrementing delivery count for redelivery");

        if (envelope.DeliveryCount >= envelope.MaxDeliveryCount)
        {
            throw new InvalidOperationException(
                $"Service Bus message '{envelope.MessageId ?? "<no-message-id>"}' for entity '{entityPath}' cannot be requeued because delivery count {envelope.DeliveryCount} has already reached MaxDeliveryCount {envelope.MaxDeliveryCount}.");
        }

        return envelope with { DeliveryCount = envelope.DeliveryCount + 1 };
    }

    internal void Requeue(string entityPath, InMemoryServiceBusEnvelope envelope)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);

        EnsureEnvelopeBelongsToEntity(entityPath, envelope);

        var pending = GetEnvelopeStore(
            _pendingMessages,
            entityPath,
            "pending-message buffer",
            "requeueing messages");

        pending.Enqueue(envelope);
    }

    internal void Complete(string entityPath, InMemoryServiceBusEnvelope envelope)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);

        EnsureEnvelopeBelongsToEntity(entityPath, envelope);

        var completed = GetEnvelopeStore(
            _completedMessages,
            entityPath,
            "completed-message buffer",
            "recording completed messages");

        completed.Enqueue(envelope);
    }

    internal void DeadLetter(string entityPath, InMemoryServiceBusEnvelope envelope, string deadLetterReason)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterReason, nameof(deadLetterReason));

        EnsureEnvelopeBelongsToEntity(entityPath, envelope);

        var deadLettered = GetOutcomeStore(
            _deadLetteredMessages,
            entityPath,
            "dead-letter buffer",
            "recording dead-lettered messages");

        deadLettered.Enqueue(CreateDeadLetterOutcome(envelope, deadLetterReason));
    }

    internal void Error(string entityPath, InMemoryServiceBusEnvelope envelope, Exception exception)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(exception);

        EnsureEnvelopeBelongsToEntity(entityPath, envelope);

        var errored = GetOutcomeStore(
            _erroredMessages,
            entityPath,
            "errored-message buffer",
            "recording errored messages");

        errored.Enqueue(CreateErroredOutcome(envelope, exception));
    }

    internal void Enqueue(string entityPath, ServiceBusMessage message)
    {
        entityPath = ValidateEntityName(entityPath, nameof(entityPath));
        ArgumentNullException.ThrowIfNull(message);

        if (_queues.ContainsKey(entityPath))
        {
            EnqueueDeclaredEntity(entityPath, message);
            return;
        }

        if (_topicSubscriptions.TryGetValue(entityPath, out var subscriptions))
        {
            foreach (var subscriptionName in subscriptions.Keys)
            {
                EnqueueDeclaredEntity(GetSubscriptionEntityPath(entityPath, subscriptionName), message);
            }

            return;
        }

        throw new InvalidOperationException(
            $"Service Bus entity '{entityPath}' has not been declared. " +
            $"Create the queue or topic through {nameof(IAzureServiceBusAdministration.CreateQueueAsync)}(...) " +
            $"or {nameof(IAzureServiceBusAdministration.CreateTopicAsync)}(...) before sending messages.");
    }

    internal static string GetSubscriptionEntityPath(string topicName, string subscriptionName) =>
        $"{topicName}/Subscriptions/{subscriptionName}";

    private void EnsureEntityStores(string entityPath)
    {
        _pendingMessages.TryAdd(entityPath, new ConcurrentQueue<InMemoryServiceBusEnvelope>());
        _completedMessages.TryAdd(entityPath, new ConcurrentQueue<InMemoryServiceBusEnvelope>());
        _deadLetteredMessages.TryAdd(entityPath, new ConcurrentQueue<InMemoryServiceBusDeadLetterOutcome>());
        _erroredMessages.TryAdd(entityPath, new ConcurrentQueue<InMemoryServiceBusErroredOutcome>());
    }

    private void EnqueueDeclaredEntity(string entityPath, ServiceBusMessage message)
    {
        var queue = GetEnvelopeStore(
            _pendingMessages,
            entityPath,
            "pending-message buffer",
            "sending messages");

        queue.Enqueue(CreateEnvelope(entityPath, message));
    }

    private static ConcurrentQueue<InMemoryServiceBusEnvelope> GetEnvelopeStore(
        ConcurrentDictionary<string, ConcurrentQueue<InMemoryServiceBusEnvelope>> store,
        string entityPath,
        string storeDescription,
        string operation)
    {
        if (!store.TryGetValue(entityPath, out var queue))
        {
            throw new InvalidOperationException(
                $"Service Bus entity '{entityPath}' topology is inconsistent because no {storeDescription} exists for it while {operation}. " +
                "Declare the queue or subscription before using it.");
        }

        return queue;
    }

    private static ConcurrentQueue<TOutcome> GetOutcomeStore<TOutcome>(
        ConcurrentDictionary<string, ConcurrentQueue<TOutcome>> store,
        string entityPath,
        string storeDescription,
        string operation)
    {
        if (!store.TryGetValue(entityPath, out var queue))
        {
            throw new InvalidOperationException(
                $"Service Bus entity '{entityPath}' topology is inconsistent because no {storeDescription} exists for it while {operation}. " +
                "Declare the queue or subscription before using it.");
        }

        return queue;
    }

    private static void EnsureEnvelopeBelongsToEntity(string entityPath, InMemoryServiceBusEnvelope envelope)
    {
        if (!string.Equals(entityPath, envelope.EntityPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot move message '{envelope.MessageId ?? "<no-message-id>"}' from entity '{envelope.EntityPath}' into '{entityPath}'.");
        }
    }

    private static void ValidateEnvelopeDeliveryMetadata(
        InMemoryServiceBusEnvelope envelope,
        string entityPath,
        string operation)
    {
        if (envelope.DeliveryCount < 1)
        {
            throw new InvalidOperationException(
                $"Service Bus message '{envelope.MessageId ?? "<no-message-id>"}' for entity '{entityPath}' has invalid delivery metadata because DeliveryCount must be at least 1 while {operation}.");
        }

        _ = ValidateMaxDeliveryCount(
            envelope.MaxDeliveryCount,
            nameof(InMemoryServiceBusEnvelope.MaxDeliveryCount),
            $"for message '{envelope.MessageId ?? "<no-message-id>"}' on entity '{entityPath}' while {operation}");
    }

    private static InMemoryServiceBusEnvelope CreateEnvelope(string entityPath, ServiceBusMessage message) =>
        new(
            EntityPath: entityPath,
            Body: CloneBody(message.Body),
            MessageId: message.MessageId,
            ApplicationProperties: CloneApplicationProperties(message.ApplicationProperties),
            EnqueuedAt: DateTimeOffset.UtcNow,
            DeliveryCount: 1,
            MaxDeliveryCount: DefaultMaxDeliveryCount);

    private static InMemoryServiceBusDeadLetterOutcome CreateDeadLetterOutcome(
        InMemoryServiceBusEnvelope envelope,
        string deadLetterReason) =>
        new(
            EntityPath: envelope.EntityPath,
            Body: CloneBody(envelope.Body),
            MessageId: envelope.MessageId,
            ApplicationProperties: CloneApplicationProperties(envelope.ApplicationProperties),
            EnqueuedAt: envelope.EnqueuedAt,
            DeliveryCount: envelope.DeliveryCount,
            MaxDeliveryCount: envelope.MaxDeliveryCount,
            DeadLetterReason: deadLetterReason);

    private static InMemoryServiceBusErroredOutcome CreateErroredOutcome(
        InMemoryServiceBusEnvelope envelope,
        Exception exception) =>
        new(
            EntityPath: envelope.EntityPath,
            Body: CloneBody(envelope.Body),
            MessageId: envelope.MessageId,
            ApplicationProperties: CloneApplicationProperties(envelope.ApplicationProperties),
            EnqueuedAt: envelope.EnqueuedAt,
            DeliveryCount: envelope.DeliveryCount,
            MaxDeliveryCount: envelope.MaxDeliveryCount,
            Exception: exception);

    private static BinaryData CloneBody(BinaryData body) => BinaryData.FromBytes(body.ToArray());

    private static Dictionary<string, object?> CloneApplicationProperties(
        IReadOnlyDictionary<string, object?> applicationProperties)
    {
        var clone = new Dictionary<string, object?>(applicationProperties.Count, StringComparer.Ordinal);
        foreach (var (key, value) in applicationProperties)
        {
            clone[key] = value;
        }

        return clone;
    }

    private static Dictionary<string, object?> CloneApplicationProperties(
        IDictionary<string, object> applicationProperties)
    {
        var clone = new Dictionary<string, object?>(applicationProperties.Count, StringComparer.Ordinal);
        foreach (var (key, value) in applicationProperties)
        {
            clone[key] = value;
        }

        return clone;
    }

    private static int ValidateMaxDeliveryCount(int maxDeliveryCount, string paramName, string context)
    {
        if (maxDeliveryCount < 1)
        {
            throw new InvalidOperationException(
                $"Service Bus delivery metadata is invalid because {paramName} must be greater than zero {context}.");
        }

        return maxDeliveryCount;
    }

    private static string ValidateEntityName(string entityName, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName, paramName);
        return entityName;
    }
}

public sealed record InMemoryServiceBusEnvelope(
    string EntityPath,
    BinaryData Body,
    string? MessageId,
    IReadOnlyDictionary<string, object?> ApplicationProperties,
    DateTimeOffset EnqueuedAt,
    int DeliveryCount,
    int MaxDeliveryCount);

public sealed record InMemoryServiceBusDeadLetterOutcome(
    string EntityPath,
    BinaryData Body,
    string? MessageId,
    IReadOnlyDictionary<string, object?> ApplicationProperties,
    DateTimeOffset EnqueuedAt,
    int DeliveryCount,
    int MaxDeliveryCount,
    string DeadLetterReason);

public sealed record InMemoryServiceBusErroredOutcome(
    string EntityPath,
    BinaryData Body,
    string? MessageId,
    IReadOnlyDictionary<string, object?> ApplicationProperties,
    DateTimeOffset EnqueuedAt,
    int DeliveryCount,
    int MaxDeliveryCount,
    Exception Exception);
