using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.ServiceBus;
using Forgate.Azure.InMemory.ServiceBus.InMemory;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.Tests.ServiceBus.InMemory;

public sealed class InMemoryServiceBusRedeliveryTests
{
    [Fact]
    public async Task QueueFailureRequeuesOnlyForTheNextExplicitRunAndIncrementsDeliveryCount()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";
        var observedDeliveryCounts = new List<int>();
        var attempt = 0;

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-201", "retry me", "tenant-r", 1);

        await using var processor = factory.CreateQueueProcessor(
            queueName,
            new AzureServiceBusProcessorOptions(MaxDeliveryCount: 3));
        processor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            observedDeliveryCounts.Add(context.DeliveryCount);
            attempt++;

            if (attempt == 1)
            {
                throw new InvalidOperationException("first delivery fails");
            }

            await context.CompleteMessageAsync(cancellationToken);
        };

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount));

        var pendingAfterFirstRun = Assert.Single(state.GetPendingMessages(queueName));
        Assert.Equal("message-201", pendingAfterFirstRun.MessageId);
        Assert.Equal("retry me", pendingAfterFirstRun.Body.ToString());
        Assert.Equal(2, pendingAfterFirstRun.DeliveryCount);
        Assert.Equal(3, pendingAfterFirstRun.MaxDeliveryCount);
        Assert.Empty(state.GetCompletedMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));

        var firstError = Assert.Single(state.GetErroredMessages(queueName));
        Assert.Equal(1, firstError.DeliveryCount);
        Assert.Equal(3, firstError.MaxDeliveryCount);
        Assert.Equal("message-201", firstError.MessageId);

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount),
            deliveryCount => Assert.Equal(2, deliveryCount));
        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));

        var completed = Assert.Single(state.GetCompletedMessages(queueName));
        Assert.Equal("message-201", completed.MessageId);
        Assert.Equal(2, completed.DeliveryCount);
        Assert.Equal(3, completed.MaxDeliveryCount);
        Assert.Single(state.GetErroredMessages(queueName));
    }

    [Fact]
    public async Task QueueFailureDeadLettersOnceConfiguredMaxDeliveryCountIsExhausted()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";
        var observedDeliveryCounts = new List<int>();

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-202", "poison order", "tenant-r", 2);

        await using var processor = factory.CreateQueueProcessor(
            queueName,
            new AzureServiceBusProcessorOptions(MaxDeliveryCount: 2));
        processor.ProcessMessageAsync = (context, _) =>
        {
            observedDeliveryCounts.Add(context.DeliveryCount);
            return Task.FromException(new InvalidOperationException($"failure {context.DeliveryCount}"));
        };

        await processor.StartProcessingAsync();

        var pendingAfterFirstRun = Assert.Single(state.GetPendingMessages(queueName));
        Assert.Equal(2, pendingAfterFirstRun.DeliveryCount);
        Assert.Equal(2, pendingAfterFirstRun.MaxDeliveryCount);
        Assert.Empty(state.GetDeadLetteredMessages(queueName));
        Assert.Empty(state.GetCompletedMessages(queueName));

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount),
            deliveryCount => Assert.Equal(2, deliveryCount));
        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetCompletedMessages(queueName));

        var deadLetter = Assert.Single(state.GetDeadLetteredMessages(queueName));
        Assert.Equal("message-202", deadLetter.MessageId);
        Assert.Equal(2, deadLetter.DeliveryCount);
        Assert.Equal(2, deadLetter.MaxDeliveryCount);
        Assert.Contains("MaxDeliveryCount exhausted", deadLetter.DeadLetterReason, StringComparison.Ordinal);
        Assert.Contains("message-202", deadLetter.DeadLetterReason, StringComparison.Ordinal);

        var errors = state.GetErroredMessages(queueName);
        Assert.Collection(
            errors,
            outcome => Assert.Equal(1, outcome.DeliveryCount),
            outcome => Assert.Equal(2, outcome.DeliveryCount));
    }

    [Fact]
    public async Task SubscriptionFailureRequeuesOnlyItsCanonicalPathForTheNextExplicitRunAndIncrementsDeliveryCount()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string retryingSubscription = "billing";
        const string siblingSubscription = "shipping";
        var retryingPath = GetSubscriptionEntityPath(topicName, retryingSubscription);
        var siblingPath = GetSubscriptionEntityPath(topicName, siblingSubscription);
        var observedDeliveryCounts = new List<int>();
        var attempt = 0;

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, retryingSubscription);
        await factory.Administration.CreateSubscriptionAsync(topicName, siblingSubscription);
        await SendMessageAsync(factory, topicName, "message-301", "retry subscription", "tenant-r", 1);

        await using var processor = factory.CreateSubscriptionProcessor(
            topicName,
            retryingSubscription,
            new AzureServiceBusProcessorOptions(MaxDeliveryCount: 3));
        processor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            observedDeliveryCounts.Add(context.DeliveryCount);
            attempt++;

            if (attempt == 1)
            {
                throw new InvalidOperationException("first subscription delivery fails");
            }

            await context.CompleteMessageAsync(cancellationToken);
        };

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount));

        var pendingAfterFirstRun = Assert.Single(state.GetPendingMessages(retryingPath));
        Assert.Equal("message-301", pendingAfterFirstRun.MessageId);
        Assert.Equal("retry subscription", pendingAfterFirstRun.Body.ToString());
        Assert.Equal(2, pendingAfterFirstRun.DeliveryCount);
        Assert.Equal(3, pendingAfterFirstRun.MaxDeliveryCount);
        Assert.Empty(state.GetCompletedMessages(retryingPath));
        Assert.Empty(state.GetDeadLetteredMessages(retryingPath));

        var firstError = Assert.Single(state.GetErroredMessages(retryingPath));
        Assert.Equal(1, firstError.DeliveryCount);
        Assert.Equal(3, firstError.MaxDeliveryCount);
        Assert.Equal("message-301", firstError.MessageId);

        var siblingPending = Assert.Single(state.GetPendingMessages(siblingPath));
        Assert.Equal("message-301", siblingPending.MessageId);
        Assert.Equal(1, siblingPending.DeliveryCount);
        Assert.Equal(10, siblingPending.MaxDeliveryCount);
        Assert.Empty(state.GetCompletedMessages(siblingPath));
        Assert.Empty(state.GetDeadLetteredMessages(siblingPath));
        Assert.Empty(state.GetErroredMessages(siblingPath));

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount),
            deliveryCount => Assert.Equal(2, deliveryCount));
        Assert.Empty(state.GetPendingMessages(retryingPath));
        Assert.Empty(state.GetDeadLetteredMessages(retryingPath));

        var completed = Assert.Single(state.GetCompletedMessages(retryingPath));
        Assert.Equal("message-301", completed.MessageId);
        Assert.Equal(2, completed.DeliveryCount);
        Assert.Equal(3, completed.MaxDeliveryCount);

        siblingPending = Assert.Single(state.GetPendingMessages(siblingPath));
        Assert.Equal(1, siblingPending.DeliveryCount);
        Assert.Equal(10, siblingPending.MaxDeliveryCount);
        Assert.Single(state.GetErroredMessages(retryingPath));
    }

    [Fact]
    public async Task SubscriptionFailureDeadLettersOnlyTheExhaustedCanonicalPathAndLeavesSiblingSubscriptionDeliverable()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string exhaustedSubscription = "billing";
        const string siblingSubscription = "shipping";
        var exhaustedPath = GetSubscriptionEntityPath(topicName, exhaustedSubscription);
        var siblingPath = GetSubscriptionEntityPath(topicName, siblingSubscription);
        var observedDeliveryCounts = new List<int>();
        var siblingObservedDeliveryCounts = new List<int>();

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, exhaustedSubscription);
        await factory.Administration.CreateSubscriptionAsync(topicName, siblingSubscription);
        await SendMessageAsync(factory, topicName, "message-302", "poison subscription", "tenant-r", 2);

        await using var exhaustedProcessor = factory.CreateSubscriptionProcessor(
            topicName,
            exhaustedSubscription,
            new AzureServiceBusProcessorOptions(MaxDeliveryCount: 2));
        exhaustedProcessor.ProcessMessageAsync = (context, _) =>
        {
            observedDeliveryCounts.Add(context.DeliveryCount);
            return Task.FromException(new InvalidOperationException($"failure {context.DeliveryCount}"));
        };

        await exhaustedProcessor.StartProcessingAsync();

        var pendingAfterFirstRun = Assert.Single(state.GetPendingMessages(exhaustedPath));
        Assert.Equal(2, pendingAfterFirstRun.DeliveryCount);
        Assert.Equal(2, pendingAfterFirstRun.MaxDeliveryCount);
        Assert.Empty(state.GetDeadLetteredMessages(exhaustedPath));
        Assert.Empty(state.GetCompletedMessages(exhaustedPath));

        var siblingPending = Assert.Single(state.GetPendingMessages(siblingPath));
        Assert.Equal(1, siblingPending.DeliveryCount);
        Assert.Equal(10, siblingPending.MaxDeliveryCount);
        Assert.Empty(state.GetDeadLetteredMessages(siblingPath));
        Assert.Empty(state.GetCompletedMessages(siblingPath));
        Assert.Empty(state.GetErroredMessages(siblingPath));

        await exhaustedProcessor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount),
            deliveryCount => Assert.Equal(2, deliveryCount));
        Assert.Empty(state.GetPendingMessages(exhaustedPath));
        Assert.Empty(state.GetCompletedMessages(exhaustedPath));

        var deadLetter = Assert.Single(state.GetDeadLetteredMessages(exhaustedPath));
        Assert.Equal("message-302", deadLetter.MessageId);
        Assert.Equal(2, deadLetter.DeliveryCount);
        Assert.Equal(2, deadLetter.MaxDeliveryCount);
        Assert.Contains("MaxDeliveryCount exhausted", deadLetter.DeadLetterReason, StringComparison.Ordinal);
        Assert.Contains("message-302", deadLetter.DeadLetterReason, StringComparison.Ordinal);

        var errors = state.GetErroredMessages(exhaustedPath);
        Assert.Collection(
            errors,
            outcome => Assert.Equal(1, outcome.DeliveryCount),
            outcome => Assert.Equal(2, outcome.DeliveryCount));

        await using var siblingProcessor = factory.CreateSubscriptionProcessor(topicName, siblingSubscription);
        siblingProcessor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            siblingObservedDeliveryCounts.Add(context.DeliveryCount);
            await context.CompleteMessageAsync(cancellationToken);
        };

        await siblingProcessor.StartProcessingAsync();

        Assert.Collection(
            siblingObservedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount));
        Assert.Empty(state.GetPendingMessages(siblingPath));
        Assert.Empty(state.GetDeadLetteredMessages(siblingPath));

        var siblingCompleted = Assert.Single(state.GetCompletedMessages(siblingPath));
        Assert.Equal("message-302", siblingCompleted.MessageId);
        Assert.Equal(1, siblingCompleted.DeliveryCount);
        Assert.Equal(10, siblingCompleted.MaxDeliveryCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QueueProcessorCreationRejectsInvalidMaxDeliveryCounts(int maxDeliveryCount)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            factory.CreateQueueProcessor(
                "orders",
                new AzureServiceBusProcessorOptions(MaxDeliveryCount: maxDeliveryCount)));

        Assert.Contains(nameof(AzureServiceBusProcessorOptions.MaxDeliveryCount), exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureServiceBusInMemory();
        return services.BuildServiceProvider();
    }

    private static string GetSubscriptionEntityPath(string topicName, string subscriptionName) =>
        $"{topicName}/Subscriptions/{subscriptionName}";

    private static async Task SendMessageAsync(
        IAzureServiceBusFactory factory,
        string entityPath,
        string messageId,
        string body,
        string tenant,
        int attempt)
    {
        await using var sender = factory.CreateSender(entityPath);
        var message = CreateMessage(messageId, body, tenant, attempt);
        await sender.SendAsync(message);
    }

    private static ServiceBusMessage CreateMessage(
        string messageId,
        string body,
        string tenant,
        int attempt)
    {
        var message = new ServiceBusMessage(BinaryData.FromString(body))
        {
            MessageId = messageId
        };
        message.ApplicationProperties["tenant"] = tenant;
        message.ApplicationProperties["attempt"] = attempt;
        return message;
    }
}
