using Azure.Messaging.ServiceBus;

namespace Azure.InMemory.ServiceBus;

public interface IAzureServiceBusFactory
{
    IAzureServiceBusSender CreateSender(
        string entityPath,
        AzureServiceBusSenderOptions? options = null);

    IAzureServiceBusProcessor CreateQueueProcessor(
        string queueName,
        AzureServiceBusProcessorOptions? options = null);

    IAzureServiceBusProcessor CreateSubscriptionProcessor(
        string topicName,
        string subscriptionName,
        AzureServiceBusProcessorOptions? options = null);

    IAzureServiceBusAdministration Administration { get; }
}

public interface IAzureServiceBusSender : IAsyncDisposable
{
    Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);

    Task SendBatchAsync(
        IReadOnlyCollection<ServiceBusMessage> messages,
        CancellationToken cancellationToken = default);
}

public interface IAzureServiceBusProcessor : IAsyncDisposable
{
    Func<AzureServiceBusReceivedMessageContext, CancellationToken, Task>? ProcessMessageAsync { get; set; }

    Func<AzureServiceBusErrorContext, CancellationToken, Task>? ProcessErrorAsync { get; set; }

    Task StartProcessingAsync(CancellationToken cancellationToken = default);

    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}

public interface IAzureServiceBusAdministration
{
    Task CreateQueueAsync(string queueName, CancellationToken cancellationToken = default);

    Task CreateTopicAsync(string topicName, CancellationToken cancellationToken = default);

    Task CreateSubscriptionAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default);
}

public sealed record AzureServiceBusSenderOptions(string? Identifier = null);

public sealed record AzureServiceBusProcessorOptions(
    int MaxConcurrentCalls = 1,
    bool AutoCompleteMessages = false,
    string? Identifier = null,
    int MaxDeliveryCount = 10);

public sealed class AzureServiceBusReceivedMessageContext
{
    private readonly Func<CancellationToken, Task> _complete;
    private readonly Func<string?, CancellationToken, Task> _deadLetter;

    public AzureServiceBusReceivedMessageContext(
        BinaryData body,
        string? messageId,
        IReadOnlyDictionary<string, object?> applicationProperties,
        int deliveryCount,
        Func<CancellationToken, Task> complete,
        Func<string?, CancellationToken, Task> deadLetter)
    {
        Body = body;
        MessageId = messageId;
        ApplicationProperties = applicationProperties;
        DeliveryCount = deliveryCount;
        _complete = complete;
        _deadLetter = deadLetter;
    }

    public BinaryData Body { get; }

    public string? MessageId { get; }

    public IReadOnlyDictionary<string, object?> ApplicationProperties { get; }

    public int DeliveryCount { get; }

    public Task CompleteMessageAsync(CancellationToken cancellationToken = default) =>
        _complete(cancellationToken);

    public Task DeadLetterMessageAsync(
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default) =>
        _deadLetter(deadLetterReason, cancellationToken);
}

public sealed record AzureServiceBusErrorContext(string EntityPath, Exception Exception);
