using Azure.InMemory.DependencyInjection;
using Azure.InMemory.ServiceBus;
using Azure.InMemory.ServiceBus.InMemory;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.InMemory.Tests.ServiceBus.InMemory;

public sealed class InMemoryServiceBusIngressTests
{
    [Fact]
    public async Task QueueSendToDeclaredQueueStoresOnePendingEnvelopeWithBodyAndMetadata()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "orders";

        Assert.False(state.QueueExists(queueName));
        Assert.Empty(state.GetPendingMessages(queueName));

        await factory.Administration.CreateQueueAsync(queueName);

        Assert.True(state.QueueExists(queueName));
        Assert.Empty(state.GetPendingMessages(queueName));

        await using var sender = factory.CreateSender(queueName);
        var message = new ServiceBusMessage(BinaryData.FromString("hello queue"))
        {
            MessageId = "message-001"
        };
        message.ApplicationProperties["tenant"] = "test-suite";
        message.ApplicationProperties["attempt"] = 2;

        await sender.SendAsync(message);

        var pendingMessages = state.GetPendingMessages(queueName);
        var envelope = Assert.Single(pendingMessages);

        Assert.Equal(queueName, envelope.EntityPath);
        Assert.Equal("hello queue", envelope.Body.ToString());
        Assert.Equal(message.MessageId, envelope.MessageId);
        Assert.Equal("test-suite", Assert.Contains("tenant", envelope.ApplicationProperties));
        Assert.Equal(2, Assert.Contains("attempt", envelope.ApplicationProperties));
        Assert.Equal(1, envelope.DeliveryCount);
        Assert.Equal(10, envelope.MaxDeliveryCount);
    }

    [Fact]
    public async Task TopicPublishFansOutBatchToDeclaredSubscriptionPathsAndLeavesTopicPathEmpty()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "orders";
        const string billingSubscription = "billing";
        const string shippingSubscription = "shipping";
        var subscriptionPaths = new[]
        {
            GetSubscriptionEntityPath(topicName, billingSubscription),
            GetSubscriptionEntityPath(topicName, shippingSubscription)
        };

        Assert.False(state.TopicExists(topicName));
        Assert.Empty(state.GetPendingMessages(topicName));

        await factory.Administration.CreateTopicAsync(topicName);
        await factory.Administration.CreateSubscriptionAsync(topicName, billingSubscription);
        await factory.Administration.CreateSubscriptionAsync(topicName, shippingSubscription);

        Assert.True(state.TopicExists(topicName));
        Assert.True(state.SubscriptionExists(topicName, billingSubscription));
        Assert.True(state.SubscriptionExists(topicName, shippingSubscription));
        Assert.Empty(state.GetPendingMessages(topicName));
        Assert.Empty(state.GetPendingMessages(subscriptionPaths[0]));
        Assert.Empty(state.GetPendingMessages(subscriptionPaths[1]));

        await using var sender = factory.CreateSender(topicName);
        var batch = new[]
        {
            CreateMessage("message-101", "hello topic #1", "tenant-a", 1),
            CreateMessage("message-102", "hello topic #2", "tenant-a", 2)
        };

        await sender.SendBatchAsync(batch);

        Assert.Empty(state.GetPendingMessages(topicName));

        var billingPending = state.GetPendingMessages(subscriptionPaths[0]);
        var shippingPending = state.GetPendingMessages(subscriptionPaths[1]);

        Assert.Equal(batch.Length, billingPending.Count);
        Assert.Equal(batch.Length, shippingPending.Count);

        AssertEnvelopeMatches(subscriptionPaths[0], batch[0], billingPending[0]);
        AssertEnvelopeMatches(subscriptionPaths[0], batch[1], billingPending[1]);
        AssertEnvelopeMatches(subscriptionPaths[1], batch[0], shippingPending[0]);
        AssertEnvelopeMatches(subscriptionPaths[1], batch[1], shippingPending[1]);

        Assert.NotSame(billingPending[0], shippingPending[0]);
        Assert.NotSame(billingPending[0].ApplicationProperties, shippingPending[0].ApplicationProperties);
    }

    [Fact]
    public async Task UnknownQueueSendFailsWithActionableDiagnostics()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string queueName = "missing-queue";

        await using var sender = factory.CreateSender(queueName);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new ServiceBusMessage(BinaryData.FromString("orphan message"))));

        Assert.Contains(queueName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IAzureServiceBusAdministration.CreateQueueAsync), exception.Message, StringComparison.Ordinal);
        Assert.False(state.QueueExists(queueName));
        Assert.Empty(state.GetPendingMessages(queueName));
    }

    [Fact]
    public async Task UnknownTopicPublishFailsWithActionableDiagnosticsAndDoesNotCreateTopicQueue()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        const string topicName = "missing-topic";

        await using var sender = factory.CreateSender(topicName);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new ServiceBusMessage(BinaryData.FromString("orphan publish"))));

        Assert.Contains(topicName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IAzureServiceBusAdministration.CreateTopicAsync), exception.Message, StringComparison.Ordinal);
        Assert.False(state.TopicExists(topicName));
        Assert.Empty(state.GetPendingMessages(topicName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task QueueCreationRejectsBlankQueueNames(string queueName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            factory.Administration.CreateQueueAsync(queueName));

        Assert.Contains("queueName", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "billing", "topicName")]
    [InlineData(" ", "billing", "topicName")]
    [InlineData("\t", "billing", "topicName")]
    [InlineData("orders", "", "subscriptionName")]
    [InlineData("orders", " ", "subscriptionName")]
    [InlineData("orders", "\t", "subscriptionName")]
    public async Task SubscriptionCreationRejectsBlankTopicOrSubscriptionNames(
        string topicName,
        string subscriptionName,
        string expectedParameterName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            factory.Administration.CreateSubscriptionAsync(topicName, subscriptionName));

        Assert.Contains(expectedParameterName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task TopicCreationRejectsBlankTopicNames(string topicName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            factory.Administration.CreateTopicAsync(topicName));

        Assert.Contains("topicName", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureServiceBusInMemory();
        return services.BuildServiceProvider();
    }

    private static string GetSubscriptionEntityPath(string topicName, string subscriptionName) =>
        $"{topicName}/Subscriptions/{subscriptionName}";

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

    private static void AssertEnvelopeMatches(
        string expectedEntityPath,
        ServiceBusMessage expectedMessage,
        InMemoryServiceBusEnvelope actualEnvelope)
    {
        Assert.Equal(expectedEntityPath, actualEnvelope.EntityPath);
        Assert.Equal(expectedMessage.Body.ToString(), actualEnvelope.Body.ToString());
        Assert.Equal(expectedMessage.MessageId, actualEnvelope.MessageId);
        Assert.Equal(
            expectedMessage.ApplicationProperties["tenant"],
            Assert.Contains("tenant", actualEnvelope.ApplicationProperties));
        Assert.Equal(
            expectedMessage.ApplicationProperties["attempt"],
            Assert.Contains("attempt", actualEnvelope.ApplicationProperties));
        Assert.Equal(1, actualEnvelope.DeliveryCount);
        Assert.Equal(10, actualEnvelope.MaxDeliveryCount);
    }
}
