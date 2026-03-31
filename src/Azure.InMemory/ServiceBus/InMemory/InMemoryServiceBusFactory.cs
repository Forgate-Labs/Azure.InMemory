using Azure.InMemory.ServiceBus;
using Azure.Messaging.ServiceBus;

namespace Azure.InMemory.ServiceBus.InMemory;

public sealed class InMemoryServiceBusFactory : IAzureServiceBusFactory
{
    public InMemoryServiceBusFactory(InMemoryServiceBusState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Administration = new InMemoryServiceBusAdministration(State);
    }

    public InMemoryServiceBusState State { get; }

    public IAzureServiceBusAdministration Administration { get; }

    public IAzureServiceBusSender CreateSender(
        string entityPath,
        AzureServiceBusSenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPath);
        return new InMemoryServiceBusSender(State, entityPath, options);
    }

    public IAzureServiceBusProcessor CreateQueueProcessor(
        string queueName,
        AzureServiceBusProcessorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return new InMemoryServiceBusProcessor(State, queueName, options);
    }

    public IAzureServiceBusProcessor CreateSubscriptionProcessor(
        string topicName,
        string subscriptionName,
        AzureServiceBusProcessorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        return new InMemoryServiceBusProcessor(
            State,
            InMemoryServiceBusState.GetSubscriptionEntityPath(topicName, subscriptionName),
            options);
    }

    private sealed class InMemoryServiceBusSender(
        InMemoryServiceBusState state,
        string entityPath,
        AzureServiceBusSenderOptions? options) : IAzureServiceBusSender
    {
        private readonly InMemoryServiceBusState _state = state ?? throw new ArgumentNullException(nameof(state));
        private readonly string _entityPath = entityPath ?? throw new ArgumentNullException(nameof(entityPath));

        public string? Identifier { get; } = options?.Identifier;

        public Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.Enqueue(_entityPath, message ?? throw new ArgumentNullException(nameof(message)));
            return Task.CompletedTask;
        }

        public Task SendBatchAsync(
            IReadOnlyCollection<ServiceBusMessage> messages,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var message in messages)
            {
                _state.Enqueue(_entityPath, message ?? throw new ArgumentNullException(nameof(messages)));
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemoryServiceBusProcessor : IAzureServiceBusProcessor
    {
        private const string DefaultDeadLetterReason = "Dead-lettered by Azure.InMemory SDK adapter.";
        private const string MaxDeliveryExceededDeadLetterReasonPrefix = "MaxDeliveryCount exhausted by Azure.InMemory SDK adapter.";
        private const int UnsettledState = 0;
        private const int CompletedState = 1;
        private const int DeadLetteredState = 2;

        private readonly InMemoryServiceBusState _state;
        private readonly string _entityPath;
        private readonly AzureServiceBusProcessorOptions _options;

        public InMemoryServiceBusProcessor(
            InMemoryServiceBusState state,
            string entityPath,
            AzureServiceBusProcessorOptions? options)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entityPath = entityPath ?? throw new ArgumentNullException(nameof(entityPath));
            _options = ValidateProcessorOptions(options ?? new AzureServiceBusProcessorOptions(), _entityPath);
        }

        public string? Identifier => _options.Identifier;

        public Func<AzureServiceBusReceivedMessageContext, CancellationToken, Task>? ProcessMessageAsync { get; set; }

        public Func<AzureServiceBusErrorContext, CancellationToken, Task>? ProcessErrorAsync { get; set; }

        public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureProcessableEntityExists();

            var handler = ProcessMessageAsync;
            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No ProcessMessageAsync handler has been configured for Service Bus entity '{_entityPath}'.");
            }

            var batch = _state.DequeuePendingBatch(_entityPath);
            foreach (var drainedEnvelope in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var envelope = InMemoryServiceBusState.PrepareForProcessing(
                    _entityPath,
                    drainedEnvelope,
                    _options.MaxDeliveryCount);
                var settlement = new InMemoryMessageSettlement(_state, _entityPath, envelope);
                var context = new AzureServiceBusReceivedMessageContext(
                    envelope.Body,
                    envelope.MessageId,
                    envelope.ApplicationProperties,
                    envelope.DeliveryCount,
                    complete: settlement.CompleteAsync,
                    deadLetter: settlement.DeadLetterAsync);

                try
                {
                    await handler(context, cancellationToken).ConfigureAwait(false);

                    if (!settlement.IsSettled)
                    {
                        if (_options.AutoCompleteMessages)
                        {
                            await settlement.CompleteAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            _state.Requeue(_entityPath, envelope);
                        }
                    }
                }
                catch (Exception exception)
                {
                    _state.Error(_entityPath, envelope, exception);
                    HandleFailedDelivery(settlement, envelope);
                    await NotifyProcessErrorAsync(exception, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public Task StopProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void EnsureProcessableEntityExists()
        {
            if (_state.ProcessorEntityExists(_entityPath))
            {
                return;
            }

            var (entityType, creationMethod) = IsSubscriptionEntityPath(_entityPath)
                ? ("subscription", nameof(IAzureServiceBusAdministration.CreateSubscriptionAsync))
                : ("queue", nameof(IAzureServiceBusAdministration.CreateQueueAsync));

            throw new InvalidOperationException(
                $"Service Bus {entityType} '{_entityPath}' has not been declared. " +
                $"Create the {entityType} through {creationMethod}(...) before starting processing.");
        }

        private void HandleFailedDelivery(
            InMemoryMessageSettlement settlement,
            InMemoryServiceBusEnvelope envelope)
        {
            if (settlement.IsSettled)
            {
                return;
            }

            if (InMemoryServiceBusState.HasReachedMaxDeliveryCount(_entityPath, envelope))
            {
                _state.DeadLetter(_entityPath, envelope, CreateMaxDeliveryExceededReason(envelope));
                return;
            }

            _state.Requeue(_entityPath, InMemoryServiceBusState.PrepareForNextDelivery(_entityPath, envelope));
        }

        private Task NotifyProcessErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            var errorHandler = ProcessErrorAsync;
            return errorHandler is null
                ? Task.CompletedTask
                : errorHandler(new AzureServiceBusErrorContext(_entityPath, exception), cancellationToken);
        }

        private static AzureServiceBusProcessorOptions ValidateProcessorOptions(
            AzureServiceBusProcessorOptions options,
            string entityPath)
        {
            if (options.MaxDeliveryCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.MaxDeliveryCount,
                    $"Service Bus processor for entity '{entityPath}' requires {nameof(AzureServiceBusProcessorOptions.MaxDeliveryCount)} to be greater than zero.");
            }

            return options;
        }

        private static string CreateMaxDeliveryExceededReason(InMemoryServiceBusEnvelope envelope) =>
            $"{MaxDeliveryExceededDeadLetterReasonPrefix} Message '{envelope.MessageId ?? "<no-message-id>"}' reached delivery count {envelope.DeliveryCount} of {envelope.MaxDeliveryCount}.";

        private static bool IsSubscriptionEntityPath(string entityPath) =>
            entityPath.Contains("/Subscriptions/", StringComparison.OrdinalIgnoreCase);

        private sealed class InMemoryMessageSettlement(
            InMemoryServiceBusState state,
            string entityPath,
            InMemoryServiceBusEnvelope envelope)
        {
            private readonly InMemoryServiceBusState _state = state ?? throw new ArgumentNullException(nameof(state));
            private readonly string _entityPath = entityPath ?? throw new ArgumentNullException(nameof(entityPath));
            private readonly InMemoryServiceBusEnvelope _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            private int _settlementState;

            public bool IsSettled => Volatile.Read(ref _settlementState) != UnsettledState;

            public Task CompleteAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Interlocked.CompareExchange(ref _settlementState, CompletedState, UnsettledState) != UnsettledState)
                {
                    throw CreateInvalidSettlementException("completed");
                }

                _state.Complete(_entityPath, _envelope);
                return Task.CompletedTask;
            }

            public Task DeadLetterAsync(string? deadLetterReason, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (deadLetterReason is not null && string.IsNullOrWhiteSpace(deadLetterReason))
                {
                    throw new ArgumentException(
                        "A dead-letter reason must be null or a non-empty value.",
                        nameof(deadLetterReason));
                }

                if (Interlocked.CompareExchange(ref _settlementState, DeadLetteredState, UnsettledState) != UnsettledState)
                {
                    throw CreateInvalidSettlementException("dead-lettered");
                }

                _state.DeadLetter(_entityPath, _envelope, deadLetterReason ?? DefaultDeadLetterReason);
                return Task.CompletedTask;
            }

            private InvalidOperationException CreateInvalidSettlementException(string attemptedAction)
            {
                var settledState = Volatile.Read(ref _settlementState) switch
                {
                    CompletedState => "completed",
                    DeadLetteredState => "dead-lettered",
                    _ => "settled"
                };

                return new InvalidOperationException(
                    $"Message '{_envelope.MessageId ?? "<no-message-id>"}' for entity '{_entityPath}' has already been {settledState} and cannot be {attemptedAction} again.");
            }
        }
    }

    private sealed class InMemoryServiceBusAdministration(InMemoryServiceBusState state) : IAzureServiceBusAdministration
    {
        private readonly InMemoryServiceBusState _state = state ?? throw new ArgumentNullException(nameof(state));

        public Task CreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.EnsureQueue(queueName);
            return Task.CompletedTask;
        }

        public Task CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.EnsureTopic(topicName);
            return Task.CompletedTask;
        }

        public Task CreateSubscriptionAsync(
            string topicName,
            string subscriptionName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.EnsureSubscription(topicName, subscriptionName);
            return Task.CompletedTask;
        }
    }
}
