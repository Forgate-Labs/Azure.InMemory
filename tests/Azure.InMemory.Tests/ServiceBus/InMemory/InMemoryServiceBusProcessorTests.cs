using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.ServiceBus;
using Forgate.Azure.InMemory.ServiceBus.InMemory;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.Tests.ServiceBus.InMemory;

public sealed class InMemoryServiceBusProcessorTests
{
    [Fact]
    public async Task QueueStartProcessingCompletesExplicitlySettledMessagesAndPreservesEnvelopeMetadata()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-001", "hello queue", "tenant-a", 2);

        await using var processor = factory.CreateQueueProcessor(queueName);
        var handledMessageIds = new List<string?>();
        processor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            handledMessageIds.Add(context.MessageId);
            Assert.Equal("hello queue", context.Body.ToString());
            Assert.Equal("tenant-a", Assert.Contains("tenant", context.ApplicationProperties));
            Assert.Equal(2, Assert.Contains("attempt", context.ApplicationProperties));
            await context.CompleteMessageAsync(cancellationToken);
        };

        await processor.StartProcessingAsync();

        Assert.Equal("message-001", Assert.Single(handledMessageIds));
        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));
        Assert.Empty(state.GetErroredMessages(queueName));

        var completed = state.GetCompletedMessages(queueName);
        var envelope = Assert.Single(completed);
        Assert.Equal(queueName, envelope.EntityPath);
        Assert.Equal("message-001", envelope.MessageId);
        Assert.Equal("hello queue", envelope.Body.ToString());
        Assert.Equal("tenant-a", Assert.Contains("tenant", envelope.ApplicationProperties));
        Assert.Equal(2, Assert.Contains("attempt", envelope.ApplicationProperties));
    }

    [Fact]
    public async Task QueueStartProcessingLeavesSuccessfulUnsettledMessagesPendingWhenAutoCompleteDisabled()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-002", "pending queue", "tenant-b", 3);

        await using var processor = factory.CreateQueueProcessor(queueName);
        processor.ProcessMessageAsync = (_, _) => Task.CompletedTask;

        await processor.StartProcessingAsync();

        var pending = state.GetPendingMessages(queueName);
        var envelope = Assert.Single(pending);
        Assert.Equal("message-002", envelope.MessageId);
        Assert.Equal("pending queue", envelope.Body.ToString());
        Assert.Empty(state.GetCompletedMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));
        Assert.Empty(state.GetErroredMessages(queueName));
    }

    [Fact]
    public async Task QueueStartProcessingAutoCompletesSuccessfulUnsettledMessagesWhenEnabled()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-003", "auto complete queue", "tenant-c", 4);

        await using var processor = factory.CreateQueueProcessor(
            queueName,
            new AzureServiceBusProcessorOptions(AutoCompleteMessages: true));
        processor.ProcessMessageAsync = (_, _) => Task.CompletedTask;

        await processor.StartProcessingAsync();

        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));
        Assert.Empty(state.GetErroredMessages(queueName));

        var completed = Assert.Single(state.GetCompletedMessages(queueName));
        Assert.Equal("message-003", completed.MessageId);
        Assert.Equal("auto complete queue", completed.Body.ToString());
    }

    [Fact]
    public async Task QueueStartProcessingFailsForUndeclaredQueuesWithActionableDiagnostics()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "missing-queue";

        await using var processor = factory.CreateQueueProcessor(queueName);
        processor.ProcessMessageAsync = (_, _) => Task.CompletedTask;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.StartProcessingAsync());

        Assert.Contains(queueName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IAzureServiceBusAdministration.CreateQueueAsync), exception.Message, StringComparison.Ordinal);
        Assert.False(state.QueueExists(queueName));
        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetCompletedMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));
        Assert.Empty(state.GetErroredMessages(queueName));
    }

    [Fact]
    public async Task SubscriptionStartProcessingFailsForUndeclaredSubscriptionsWithActionableDiagnostics()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string subscriptionName = "billing";
        var entityPath = GetSubscriptionEntityPath(topicName, subscriptionName);

        await using var processor = factory.CreateSubscriptionProcessor(topicName, subscriptionName);
        processor.ProcessMessageAsync = (_, _) => Task.CompletedTask;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.StartProcessingAsync());

        Assert.Contains(entityPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IAzureServiceBusAdministration.CreateSubscriptionAsync), exception.Message, StringComparison.Ordinal);
        Assert.False(state.SubscriptionExists(topicName, subscriptionName));
        Assert.Empty(state.GetPendingMessages(entityPath));
        Assert.Empty(state.GetCompletedMessages(entityPath));
        Assert.Empty(state.GetDeadLetteredMessages(entityPath));
        Assert.Empty(state.GetErroredMessages(entityPath));
    }

    [Fact]
    public async Task SubscriptionStartProcessingDeadLettersPublishedMessagesOnCanonicalPathAndPreservesReason()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string subscriptionName = "billing";
        const string deadLetterReason = "poison-order";
        var entityPath = GetSubscriptionEntityPath(topicName, subscriptionName);

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, subscriptionName);
        await SendMessageAsync(factory, topicName, "message-101", "hello topic", "tenant-s", 1);

        await using var processor = factory.CreateSubscriptionProcessor(topicName, subscriptionName);
        processor.ProcessMessageAsync = (context, cancellationToken) =>
            context.DeadLetterMessageAsync(deadLetterReason, cancellationToken);

        await processor.StartProcessingAsync();

        Assert.Empty(state.GetPendingMessages(topicName));
        Assert.Empty(state.GetPendingMessages(entityPath));
        Assert.Empty(state.GetCompletedMessages(entityPath));
        Assert.Empty(state.GetErroredMessages(entityPath));

        var outcome = Assert.Single(state.GetDeadLetteredMessages(entityPath));
        Assert.Equal(entityPath, outcome.EntityPath);
        Assert.Equal("message-101", outcome.MessageId);
        Assert.Equal("hello topic", outcome.Body.ToString());
        Assert.Equal("tenant-s", Assert.Contains("tenant", outcome.ApplicationProperties));
        Assert.Equal(1, Assert.Contains("attempt", outcome.ApplicationProperties));
        Assert.Equal(deadLetterReason, outcome.DeadLetterReason);
        Assert.Empty(state.GetDeadLetteredMessages(topicName));
    }

    [Fact]
    public async Task SubscriptionStartProcessingRecordsHandlerExceptionsInvokesProcessErrorAsyncAndRequeuesForTheNextExplicitRun()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string subscriptionName = "shipping";
        var entityPath = GetSubscriptionEntityPath(topicName, subscriptionName);
        var failure = new InvalidOperationException("boom from subscription handler");
        AzureServiceBusErrorContext? capturedError = null;
        var attempt = 0;

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, subscriptionName);
        await SendMessageAsync(factory, topicName, "message-102", "explode topic", "tenant-e", 2);

        await using var processor = factory.CreateSubscriptionProcessor(topicName, subscriptionName);
        processor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            attempt++;

            if (attempt == 1)
            {
                throw failure;
            }

            await context.CompleteMessageAsync(cancellationToken);
        };
        processor.ProcessErrorAsync = (context, _) =>
        {
            capturedError = context;
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();

        Assert.NotNull(capturedError);
        Assert.Equal(entityPath, capturedError.EntityPath);
        Assert.Same(failure, capturedError.Exception);
        Assert.Empty(state.GetPendingMessages(topicName));
        Assert.Empty(state.GetCompletedMessages(entityPath));
        Assert.Empty(state.GetDeadLetteredMessages(entityPath));

        var pending = Assert.Single(state.GetPendingMessages(entityPath));
        Assert.Equal(entityPath, pending.EntityPath);
        Assert.Equal("message-102", pending.MessageId);
        Assert.Equal("explode topic", pending.Body.ToString());
        Assert.Equal("tenant-e", Assert.Contains("tenant", pending.ApplicationProperties));
        Assert.Equal(2, Assert.Contains("attempt", pending.ApplicationProperties));
        Assert.Equal(2, pending.DeliveryCount);
        Assert.Equal(10, pending.MaxDeliveryCount);

        var outcome = Assert.Single(state.GetErroredMessages(entityPath));
        Assert.Equal(entityPath, outcome.EntityPath);
        Assert.Equal("message-102", outcome.MessageId);
        Assert.Equal("explode topic", outcome.Body.ToString());
        Assert.Equal("tenant-e", Assert.Contains("tenant", outcome.ApplicationProperties));
        Assert.Equal(2, Assert.Contains("attempt", outcome.ApplicationProperties));
        Assert.Equal(1, outcome.DeliveryCount);
        Assert.Equal(10, outcome.MaxDeliveryCount);
        Assert.Same(failure, outcome.Exception);
        Assert.Empty(state.GetErroredMessages(topicName));

        await processor.StartProcessingAsync();

        Assert.Empty(state.GetPendingMessages(entityPath));
        Assert.Empty(state.GetDeadLetteredMessages(entityPath));
        Assert.Single(state.GetErroredMessages(entityPath));

        var completed = Assert.Single(state.GetCompletedMessages(entityPath));
        Assert.Equal("message-102", completed.MessageId);
        Assert.Equal(2, completed.DeliveryCount);
        Assert.Equal(10, completed.MaxDeliveryCount);
    }

    [Fact]
    public async Task QueueStartProcessingRecordsHandlerExceptionsAndRequeuesForNextExplicitRunWhenProcessErrorAsyncIsNotConfigured()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";
        var failure = new InvalidOperationException("boom without callback");

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-103", "explode queue", "tenant-f", 3);

        await using var processor = factory.CreateQueueProcessor(queueName);
        processor.ProcessMessageAsync = (context, _) =>
        {
            Assert.Equal(1, context.DeliveryCount);
            return Task.FromException(failure);
        };

        await processor.StartProcessingAsync();

        var pending = Assert.Single(state.GetPendingMessages(queueName));
        Assert.Equal("message-103", pending.MessageId);
        Assert.Equal("explode queue", pending.Body.ToString());
        Assert.Equal(2, pending.DeliveryCount);
        Assert.Equal(10, pending.MaxDeliveryCount);
        Assert.Empty(state.GetCompletedMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));

        var outcome = Assert.Single(state.GetErroredMessages(queueName));
        Assert.Equal(queueName, outcome.EntityPath);
        Assert.Equal("message-103", outcome.MessageId);
        Assert.Equal("explode queue", outcome.Body.ToString());
        Assert.Equal(1, outcome.DeliveryCount);
        Assert.Equal(10, outcome.MaxDeliveryCount);
        Assert.Same(failure, outcome.Exception);
    }

    [Fact]
    public async Task SubscriptionStartProcessingCapturesInvalidSettlementOrderingWithoutDuplicatingDeadLetterOutcome()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string subscriptionName = "fraud";
        const string deadLetterReason = "fraud-detected";
        var entityPath = GetSubscriptionEntityPath(topicName, subscriptionName);
        AzureServiceBusErrorContext? capturedError = null;

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, subscriptionName);
        await SendMessageAsync(factory, topicName, "message-104", "double settle", "tenant-g", 4);

        await using var processor = factory.CreateSubscriptionProcessor(topicName, subscriptionName);
        processor.ProcessMessageAsync = async (context, cancellationToken) =>
        {
            await context.DeadLetterMessageAsync(deadLetterReason, cancellationToken);
            await context.CompleteMessageAsync(cancellationToken);
        };
        processor.ProcessErrorAsync = (context, _) =>
        {
            capturedError = context;
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();

        Assert.NotNull(capturedError);
        Assert.Equal(entityPath, capturedError.EntityPath);
        Assert.IsType<InvalidOperationException>(capturedError.Exception);
        Assert.Contains("dead-lettered", capturedError.Exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.GetPendingMessages(entityPath));
        Assert.Empty(state.GetCompletedMessages(entityPath));

        var deadLetterOutcome = Assert.Single(state.GetDeadLetteredMessages(entityPath));
        Assert.Equal("message-104", deadLetterOutcome.MessageId);
        Assert.Equal(deadLetterReason, deadLetterOutcome.DeadLetterReason);

        var errorOutcome = Assert.Single(state.GetErroredMessages(entityPath));
        Assert.Equal("message-104", errorOutcome.MessageId);
        Assert.Same(capturedError.Exception, errorOutcome.Exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void QueueProcessorCreationRejectsBlankQueueNames(string queueName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        var exception = Assert.Throws<ArgumentException>(() => factory.CreateQueueProcessor(queueName));

        Assert.Contains("queueName", exception.Message, StringComparison.Ordinal);
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
