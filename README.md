# Azure.InMemory

Azure.InMemory packages internal-friendly seams for Azure dependencies so application code can resolve focused factories from DI and switch between SDK-backed and in-memory providers intentionally.

This package includes seams for Service Bus, Blob Storage, and Key Vault. The quickstart below focuses on the in-memory Service Bus provider because it is the current package-facing seam another internal team can use immediately without external infrastructure.

## Install

Add the package from your internal NuGet feed:

```xml
<PackageReference Include="Azure.InMemory" Version="1.0.0" />
```

```bash
dotnet add package Azure.InMemory --version 1.0.0
```

This README is written for package consumers, not repo contributors. It assumes the package is already available on your feed. A fresh local-feed consumer proof is a separate acceptance step, not a hidden prerequisite for using the API shown here.

## Service Bus seam at a glance

The Service Bus API is intentionally centered on `IAzureServiceBusFactory` instead of exposing raw Azure SDK clients from the application seam.

- Register exactly one Service Bus backend in DI for a given `IServiceCollection`.
- Resolve `IAzureServiceBusFactory` where your code sends messages, creates processors, or declares topology.
- Declare queues, topics, and subscriptions explicitly through `factory.Administration` before sending or processing.
- Use `CreateQueueProcessor(...)` and `CreateSubscriptionProcessor(...)` for consumers.
- Use `CreateSender(...)` for queues and topics.

For in-memory tests or infrastructure-free local flows, register the package with `AddAzureServiceBusInMemory()`.

## Quickstart: register and use the in-memory Service Bus provider

```csharp
using Azure.InMemory.DependencyInjection;
using Azure.InMemory.ServiceBus;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAzureServiceBusInMemory();

using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

const string queueName = "orders";

await factory.Administration.CreateQueueAsync(queueName);

await using (var sender = factory.CreateSender(queueName))
{
    await sender.SendAsync(new ServiceBusMessage("hello from Azure.InMemory")
    {
        MessageId = "message-001"
    });
}

await using var processor = factory.CreateQueueProcessor(queueName);
processor.ProcessMessageAsync = async (message, cancellationToken) =>
{
    Console.WriteLine($"Handling {message.MessageId}: {message.Body}");
    await message.CompleteMessageAsync(cancellationToken);
};
processor.ProcessErrorAsync = (error, _) =>
{
    Console.Error.WriteLine($"Processor error on {error.EntityPath}: {error.Exception.Message}");
    return Task.CompletedTask;
};

await processor.StartProcessingAsync();
```

### Why the topology call matters

The in-memory provider is explicit about topology to match the package seam truthfully:

- `CreateQueueAsync(...)` must run before queue send or queue processing.
- `CreateTopicAsync(...)` and `CreateSubscriptionAsync(...)` must run before topic publish or subscription processing.
- If you skip topology declaration, the in-memory provider throws an actionable `InvalidOperationException` instead of silently inventing entities.

## Topic and subscription quickstart

For topic fan-out, publish to the topic path and process each subscription independently.

```csharp
using Azure.InMemory.ServiceBus;
using Azure.Messaging.ServiceBus;

const string topicName = "orders";
const string subscriptionName = "billing";

await factory.Administration.CreateTopicAsync(topicName);
await factory.Administration.CreateSubscriptionAsync(topicName, subscriptionName);

await using (var sender = factory.CreateSender(topicName))
{
    await sender.SendAsync(new ServiceBusMessage("order published")
    {
        MessageId = "message-101"
    });
}

await using var processor = factory.CreateSubscriptionProcessor(topicName, subscriptionName);
processor.ProcessMessageAsync = async (message, cancellationToken) =>
{
    Console.WriteLine($"Billing saw {message.MessageId} on delivery {message.DeliveryCount}");
    await message.CompleteMessageAsync(cancellationToken);
};

await processor.StartProcessingAsync();
```

Published topic messages are observable on canonical subscription entity paths in the form `<topic>/Subscriptions/<subscription>`. They are not processed from the topic name itself.

## Settlement, auto-complete, and deterministic redelivery

By default, successful handler completion does **not** settle a message for you.

- Call `CompleteMessageAsync(...)` when your handler has finished successfully and you want the message recorded as completed.
- Call `DeadLetterMessageAsync(...)` when you want the message moved to the dead-letter bucket.
- If a handler returns successfully without `CompleteMessageAsync(...)`, the message stays pending unless you opted into `AutoCompleteMessages: true`.
- If you create the processor with `new AzureServiceBusProcessorOptions(AutoCompleteMessages: true)`, successful-but-unsettled messages are completed automatically.
- If the handler throws, the failed delivery is recorded as an errored outcome and the message is requeued for the next explicit `StartProcessingAsync()` run.
- There is no background retry loop. Redelivery is deterministic: failed deliveries reappear only when you call `StartProcessingAsync()` again.
- `MaxDeliveryCount` is per processor configuration. Once a message exhausts that count, the in-memory provider dead-letters it instead of requeueing again.

Example auto-complete configuration:

```csharp
await using var processor = factory.CreateQueueProcessor(
    queueName,
    new AzureServiceBusProcessorOptions(AutoCompleteMessages: true, MaxDeliveryCount: 3));
```

## Test-only inspection with `InMemoryServiceBusState`

`InMemoryServiceBusState` is a **test-only inspection surface** that `AddAzureServiceBusInMemory()` registers for observability. It is useful for assertions and diagnostics, but it is not the primary application seam. Production-facing code should depend on `IAzureServiceBusFactory` instead.

```csharp
using Azure.InMemory.ServiceBus.InMemory;

var state = provider.GetRequiredService<InMemoryServiceBusState>();

var pendingQueueMessages = state.GetPendingMessages("orders");
var completedQueueMessages = state.GetCompletedMessages("orders");
var queueErrors = state.GetErroredMessages("orders");

var subscriptionPath = "orders/Subscriptions/billing";
var subscriptionPendingMessages = state.GetPendingMessages(subscriptionPath);
var subscriptionDeadLetters = state.GetDeadLetteredMessages(subscriptionPath);
```

Use the canonical entity path string `<topic>/Subscriptions/<subscription>` when inspecting subscription outcomes. Queue inspection stays keyed by the queue name.

## When to use the SDK-backed registration instead

When your host should talk to real Azure Service Bus, register the SDK-backed mode instead:

```csharp
services.AddAzureServiceBusSdk();
```

That registration expects the host to have already registered `ServiceBusClient` and `ServiceBusAdministrationClient` in DI. The consuming application code can keep the same `IAzureServiceBusFactory` seam while the backend changes.

## Scope of this quickstart

This guide intentionally stays at the package boundary:

- it shows the real public namespaces and DI registration methods,
- it documents the deterministic in-memory Service Bus behavior that the current tests prove,
- and it avoids repo-only setup assumptions.

Fresh consumer-project restore/use proof through a local package feed remains a separate acceptance step.
