using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.ServiceBus;
using Forgate.Azure.InMemory.ServiceBus.InMemory;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.ExternalConsumer;

public sealed class ExternalConsumerQueueRedeliveryTests
{
    [Fact]
    public async Task QueueFailureRequiresExplicitSecondRunToCompleteAndExposesRetryStateFromPackageConsumerBoundary()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";
        var observedDeliveryCounts = new List<int>();
        var observedErrors = new List<AzureServiceBusErrorContext>();
        var attempt = 0;

        await factory.Administration.CreateQueueAsync(queueName);
        await SendMessageAsync(factory, queueName, "message-201", "retry me");

        await using var processor = factory.CreateQueueProcessor(
            queueName,
            new AzureServiceBusProcessorOptions(MaxDeliveryCount: 3));
        processor.ProcessMessageAsync = async (message, cancellationToken) =>
        {
            observedDeliveryCounts.Add(message.DeliveryCount);
            attempt++;

            if (attempt == 1)
            {
                throw new InvalidOperationException("first delivery fails from the external consumer");
            }

            await message.CompleteMessageAsync(cancellationToken);
        };
        processor.ProcessErrorAsync = (error, _) =>
        {
            observedErrors.Add(error);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount));
        Assert.Collection(
            observedErrors,
            error =>
            {
                Assert.Equal(queueName, error.EntityPath);
                Assert.Equal("first delivery fails from the external consumer", error.Exception.Message);
            });

        var pendingAfterFirstRun = Assert.Single(state.GetPendingMessages(queueName));
        Assert.Equal("message-201", pendingAfterFirstRun.MessageId);
        Assert.Equal("retry me", pendingAfterFirstRun.Body.ToString());
        Assert.Equal(2, pendingAfterFirstRun.DeliveryCount);
        Assert.Equal(3, pendingAfterFirstRun.MaxDeliveryCount);

        var firstErrorOutcome = Assert.Single(state.GetErroredMessages(queueName));
        Assert.Equal("message-201", firstErrorOutcome.MessageId);
        Assert.Equal(1, firstErrorOutcome.DeliveryCount);
        Assert.Equal(3, firstErrorOutcome.MaxDeliveryCount);
        Assert.Equal("first delivery fails from the external consumer", firstErrorOutcome.Exception.Message);

        Assert.Empty(state.GetCompletedMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));

        await processor.StartProcessingAsync();

        Assert.Collection(
            observedDeliveryCounts,
            deliveryCount => Assert.Equal(1, deliveryCount),
            deliveryCount => Assert.Equal(2, deliveryCount));
        Assert.Single(observedErrors);
        Assert.Empty(state.GetPendingMessages(queueName));
        Assert.Empty(state.GetDeadLetteredMessages(queueName));

        var completed = Assert.Single(state.GetCompletedMessages(queueName));
        Assert.Equal("message-201", completed.MessageId);
        Assert.Equal(2, completed.DeliveryCount);
        Assert.Equal(3, completed.MaxDeliveryCount);
        Assert.Single(state.GetErroredMessages(queueName));
    }

    [Fact]
    public async Task StartProcessingAsyncRejectsUndeclaredQueueTopologyWithPackageFacingGuidance()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        await using var processor = factory.CreateQueueProcessor("orders");
        processor.ProcessMessageAsync = (_, _) => Task.CompletedTask;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.StartProcessingAsync());

        Assert.Contains("orders", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IAzureServiceBusAdministration.CreateQueueAsync), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessingTheWrongDeclaredQueueLeavesTheExpectedQueueMessagePending()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string expectedQueueName = "orders";
        const string wrongQueueName = "payments";
        var observedMessageIds = new List<string?>();

        await factory.Administration.CreateQueueAsync(expectedQueueName);
        await factory.Administration.CreateQueueAsync(wrongQueueName);
        await SendMessageAsync(factory, expectedQueueName, "message-202", "process the right queue");

        await using var processor = factory.CreateQueueProcessor(wrongQueueName);
        processor.ProcessMessageAsync = async (message, cancellationToken) =>
        {
            observedMessageIds.Add(message.MessageId);
            await message.CompleteMessageAsync(cancellationToken);
        };

        await processor.StartProcessingAsync();

        Assert.Empty(observedMessageIds);

        var pending = Assert.Single(state.GetPendingMessages(expectedQueueName));
        Assert.Equal("message-202", pending.MessageId);
        Assert.Equal(1, pending.DeliveryCount);
        Assert.Empty(state.GetCompletedMessages(expectedQueueName));
        Assert.Empty(state.GetErroredMessages(expectedQueueName));
        Assert.Empty(state.GetPendingMessages(wrongQueueName));
        Assert.Empty(state.GetCompletedMessages(wrongQueueName));
        Assert.Empty(state.GetErroredMessages(wrongQueueName));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureServiceBusInMemory();
        return services.BuildServiceProvider();
    }

    private static async Task SendMessageAsync(
        IAzureServiceBusFactory factory,
        string entityPath,
        string messageId,
        string body)
    {
        await using var sender = factory.CreateSender(entityPath);
        var message = new ServiceBusMessage(BinaryData.FromString(body))
        {
            MessageId = messageId
        };
        message.ApplicationProperties["tenant"] = "external-consumer";
        message.ApplicationProperties["attempt"] = 1;

        await sender.SendAsync(message);
    }
}
