using Azure.InMemory.ServiceBus;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Azure.InMemory.ServiceBus.Sdk;

public sealed class AzureServiceBusSdkFactory : IAzureServiceBusFactory
{
    private readonly ServiceBusClient _client;

    public AzureServiceBusSdkFactory(
        ServiceBusClient client,
        ServiceBusAdministrationClient administrationClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Administration = new AzureServiceBusSdkAdministration(
            administrationClient ?? throw new ArgumentNullException(nameof(administrationClient)));
    }

    public IAzureServiceBusAdministration Administration { get; }

    public IAzureServiceBusSender CreateSender(
        string entityPath,
        AzureServiceBusSenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPath);

        var sender = options is null
            ? _client.CreateSender(entityPath)
            : _client.CreateSender(
                entityPath,
                new ServiceBusSenderOptions
                {
                    Identifier = options.Identifier
                });

        return new AzureServiceBusSdkSender(sender);
    }

    public IAzureServiceBusProcessor CreateQueueProcessor(
        string queueName,
        AzureServiceBusProcessorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return new AzureServiceBusSdkProcessor(
            _client.CreateProcessor(queueName, CreateProcessorOptions(options, queueName)),
            queueName);
    }

    public IAzureServiceBusProcessor CreateSubscriptionProcessor(
        string topicName,
        string subscriptionName,
        AzureServiceBusProcessorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        var entityPath = $"{topicName}/Subscriptions/{subscriptionName}";

        return new AzureServiceBusSdkProcessor(
            _client.CreateProcessor(topicName, subscriptionName, CreateProcessorOptions(options, entityPath)),
            entityPath);
    }

    private static ServiceBusProcessorOptions CreateProcessorOptions(
        AzureServiceBusProcessorOptions? options,
        string entityPath)
    {
        options ??= new AzureServiceBusProcessorOptions();

        if (options.MaxDeliveryCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxDeliveryCount,
                $"Service Bus processor for entity '{entityPath}' requires {nameof(AzureServiceBusProcessorOptions.MaxDeliveryCount)} to be greater than zero.");
        }

        return new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = options.AutoCompleteMessages,
            Identifier = options.Identifier,
            MaxConcurrentCalls = options.MaxConcurrentCalls
        };
    }

    private sealed class AzureServiceBusSdkSender : IAzureServiceBusSender
    {
        private readonly ServiceBusSender _sender;

        public AzureServiceBusSdkSender(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            return _sender.SendMessageAsync(message, cancellationToken);
        }

        public Task SendBatchAsync(
            IReadOnlyCollection<ServiceBusMessage> messages,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);

            return messages.Count == 0
                ? Task.CompletedTask
                : _sender.SendMessagesAsync(messages, cancellationToken);
        }

        public ValueTask DisposeAsync() => _sender.DisposeAsync();
    }

    private sealed class AzureServiceBusSdkProcessor : IAzureServiceBusProcessor
    {
        private readonly ServiceBusProcessor _processor;
        private readonly string _entityPath;

        public AzureServiceBusSdkProcessor(ServiceBusProcessor processor, string entityPath)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _entityPath = entityPath ?? throw new ArgumentNullException(nameof(entityPath));

            _processor.ProcessMessageAsync += OnProcessMessageAsync;
            _processor.ProcessErrorAsync += OnProcessErrorAsync;
        }

        public Func<AzureServiceBusReceivedMessageContext, CancellationToken, Task>? ProcessMessageAsync { get; set; }

        public Func<AzureServiceBusErrorContext, CancellationToken, Task>? ProcessErrorAsync { get; set; }

        public Task StartProcessingAsync(CancellationToken cancellationToken = default) =>
            _processor.StartProcessingAsync(cancellationToken);

        public Task StopProcessingAsync(CancellationToken cancellationToken = default) =>
            _processor.StopProcessingAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            _processor.ProcessMessageAsync -= OnProcessMessageAsync;
            _processor.ProcessErrorAsync -= OnProcessErrorAsync;
            await _processor.DisposeAsync().ConfigureAwait(false);
        }

        private Task OnProcessMessageAsync(ProcessMessageEventArgs args)
        {
            var handler = ProcessMessageAsync;
            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No ProcessMessageAsync handler has been configured for Service Bus entity '{_entityPath}'.");
            }

            var context = new AzureServiceBusReceivedMessageContext(
                args.Message.Body,
                args.Message.MessageId,
                CloneApplicationProperties(args.Message.ApplicationProperties),
                args.Message.DeliveryCount,
                complete: cancellationToken => args.CompleteMessageAsync(args.Message, cancellationToken),
                deadLetter: (deadLetterReason, cancellationToken) => args.DeadLetterMessageAsync(
                    args.Message,
                    deadLetterReason ?? "Dead-lettered by Azure.InMemory SDK adapter.",
                    string.Empty,
                    cancellationToken));

            return handler(context, args.CancellationToken);
        }

        private Task OnProcessErrorAsync(ProcessErrorEventArgs args)
        {
            var handler = ProcessErrorAsync;
            return handler is null
                ? Task.CompletedTask
                : handler(
                    new AzureServiceBusErrorContext(args.EntityPath ?? _entityPath, args.Exception),
                    args.CancellationToken);
        }

        private static Dictionary<string, object?> CloneApplicationProperties(
            IReadOnlyDictionary<string, object> applicationProperties)
        {
            var clone = new Dictionary<string, object?>(applicationProperties.Count, StringComparer.Ordinal);
            foreach (var (key, value) in applicationProperties)
            {
                clone[key] = value;
            }

            return clone;
        }
    }

    private sealed class AzureServiceBusSdkAdministration : IAzureServiceBusAdministration
    {
        private readonly ServiceBusAdministrationClient _administrationClient;

        public AzureServiceBusSdkAdministration(ServiceBusAdministrationClient administrationClient)
        {
            _administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
        }

        public async Task CreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            await _administrationClient.CreateQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            await _administrationClient.CreateTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateSubscriptionAsync(
            string topicName,
            string subscriptionName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

            await _administrationClient.CreateSubscriptionAsync(
                    topicName,
                    subscriptionName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
