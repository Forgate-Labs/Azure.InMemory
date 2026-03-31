# Forgate.Azure.InMemory

Forgate.Azure.InMemory is a .NET library that puts small, DI-friendly seams in front of Azure dependencies. It lets your application code depend on focused abstractions while switching the backend between the real Azure SDK and deterministic in-memory implementations without rewriting the consuming code.

The package is built for teams that want faster tests, simpler local development, and clearer boundaries around Azure resources. Instead of spreading raw SDK clients across the codebase, you register a provider once, resolve a factory from DI, and keep the application seam stable whether the host is talking to Azure or running fully in memory.

This package currently includes seams for Service Bus, Blob Storage, and Key Vault. The quickstart below starts with the in-memory Service Bus provider because it is the most complete package-facing flow and shows the intended usage pattern clearly. The same seam pattern is also available for Blob Storage and Key Vault later in this document.

## Install

Install the package from NuGet:

- NuGet Gallery: https://www.nuget.org/packages/Forgate.Azure.InMemory
- .NET CLI:

```bash
dotnet add package Forgate.Azure.InMemory --version 1.0.0
```

- Package Manager Console:

```powershell
NuGet\Install-Package Forgate.Azure.InMemory -Version 1.0.0
```

- PackageReference:

```xml
<PackageReference Include="Forgate.Azure.InMemory" Version="1.0.0" />
```

This README is written for package consumers, not repo contributors. It assumes the package is already available on NuGet. A fresh local-feed consumer proof is a separate acceptance step, not a hidden prerequisite for using the API shown here.

## Resource seams at a glance

Each Azure resource chooses its backend independently.

| Resource | Application seam | In-memory registration | SDK-backed registration | Required Azure SDK client in DI |
| --- | --- | --- | --- | --- |
| Service Bus | `IAzureServiceBusFactory` | `AddAzureServiceBusInMemory()` | `AddAzureServiceBusSdk()` | `ServiceBusClient` and `ServiceBusAdministrationClient` |
| Blob Storage | `IAzureBlobFactory` | `AddAzureBlobInMemory()` | `AddAzureBlobSdk()` | `BlobServiceClient` |
| Key Vault | `IAzureKeyVaultFactory` | `AddAzureKeyVaultInMemory()` | `AddAzureKeyVaultSdk()` | `SecretClient` |

Rules that apply across the package:

- Register exactly one backend per resource in a given `IServiceCollection`.
- You can mix backends across resources, for example Service Bus in memory with Blob SDK and Key Vault in memory.
- Your consuming code stays on the package seam (`IAzureServiceBusFactory`, `IAzureBlobFactory`, `IAzureKeyVaultFactory`) while the backend changes underneath.

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
using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.ServiceBus;
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
    await sender.SendAsync(new ServiceBusMessage("hello from Forgate.Azure.InMemory")
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
using Forgate.Azure.InMemory.ServiceBus;
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
using Forgate.Azure.InMemory.ServiceBus.InMemory;

var state = provider.GetRequiredService<InMemoryServiceBusState>();

var pendingQueueMessages = state.GetPendingMessages("orders");
var completedQueueMessages = state.GetCompletedMessages("orders");
var queueErrors = state.GetErroredMessages("orders");

var subscriptionPath = "orders/Subscriptions/billing";
var subscriptionPendingMessages = state.GetPendingMessages(subscriptionPath);
var subscriptionDeadLetters = state.GetDeadLetteredMessages(subscriptionPath);
```

Use the canonical entity path string `<topic>/Subscriptions/<subscription>` when inspecting subscription outcomes. Queue inspection stays keyed by the queue name.

## When to use the SDK-backed Service Bus registration instead

When your host should talk to real Azure Service Bus, register the SDK-backed mode instead:

```csharp
services.AddAzureServiceBusSdk();
```

That registration expects the host to have already registered `ServiceBusClient` and `ServiceBusAdministrationClient` in DI. The consuming application code can keep the same `IAzureServiceBusFactory` seam while the backend changes.

## Blob Storage seam at a glance

The Blob Storage API is centered on `IAzureBlobFactory` and keeps application code away from raw `BlobServiceClient` usage.

- Register exactly one Blob backend in DI for a given `IServiceCollection`.
- Resolve `IAzureBlobFactory` where your code needs container and blob operations.
- Use `GetContainer(containerName)` to obtain an `IAzureBlobContainer`.
- Use `GetBlobClient(blobName)` to obtain an `IAzureBlobClient`.
- Use `UploadAsync(...)`, `ExistsAsync(...)`, and `DownloadAsync(...)` on the blob seam.

For in-memory tests or infrastructure-free local flows, register the package with `AddAzureBlobInMemory()`.

## Quickstart: register and use the in-memory Blob provider

```csharp
using Forgate.Azure.InMemory.Blob;
using Forgate.Azure.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAzureBlobInMemory();

using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IAzureBlobFactory>();

var container = factory.GetContainer("reports");
var blob = container.GetBlobClient("daily.json");

await blob.UploadAsync(
    BinaryData.FromString("{\"status\":\"ok\"}"),
    overwrite: false,
    contentType: "application/json");

var exists = await blob.ExistsAsync();
var download = await blob.DownloadAsync();

Console.WriteLine($"Blob exists: {exists}");
Console.WriteLine(download?.Content.ToString());
Console.WriteLine(download?.ContentType);
```

### Blob behavior that the in-memory provider guarantees

The in-memory Blob provider is intentionally small and deterministic:

- `GetContainer("name")` establishes that container namespace inside the in-memory state root.
- Missing blobs return `false` from `ExistsAsync()` and `null` from `DownloadAsync()`.
- Uploading the same blob twice with `overwrite: false` throws an actionable `InvalidOperationException`.
- Re-uploading with `overwrite: true` replaces both content and `contentType`.
- Container names and blob names are treated case-insensitively by the in-memory provider.

One important seam tradeoff: the package does **not** expose explicit container creation APIs. In SDK-backed mode, that means the real Azure container lifecycle is still your host's responsibility. In in-memory mode, calling `GetContainer(...)` is enough to establish the namespace for local/test use.

## Test-only inspection with `InMemoryBlobState`

`InMemoryBlobState` is a **test-only inspection surface** that `AddAzureBlobInMemory()` registers for observability. Production-facing code should depend on `IAzureBlobFactory` instead.

```csharp
using Forgate.Azure.InMemory.Blob.InMemory;

var state = provider.GetRequiredService<InMemoryBlobState>();

var containerExists = state.ContainerExists("reports");
var blobExists = state.BlobExists("reports", "daily.json");
var logicalContainerNames = state.ContainerNames;
```

## When to use the SDK-backed Blob registration instead

When your host should talk to real Azure Blob Storage, register the SDK-backed mode instead:

```csharp
services.AddAzureBlobSdk();
```

That registration expects the host to have already registered `BlobServiceClient` in DI. The consuming application code can keep the same `IAzureBlobFactory` seam while the backend changes.

## Key Vault seam at a glance

The Key Vault API is centered on `IAzureKeyVaultFactory` and `IAzureKeyVaultSecretStore` instead of exposing raw `SecretClient` usage from your application seam.

- Register exactly one Key Vault backend in DI for a given `IServiceCollection`.
- Resolve `IAzureKeyVaultFactory` where your code needs secret access.
- Call `GetSecretStore()` to obtain an `IAzureKeyVaultSecretStore`.
- Use `SetSecretAsync(...)` and `GetSecretAsync(...)` on the secret store seam.

For in-memory tests or infrastructure-free local flows, register the package with `AddAzureKeyVaultInMemory()`.

## Quickstart: register and use the in-memory Key Vault provider

```csharp
using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.KeyVault;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAzureKeyVaultInMemory();

using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IAzureKeyVaultFactory>();
var store = factory.GetSecretStore();

var stored = await store.SetSecretAsync("api-key", "super-secret-value");
var retrieved = await store.GetSecretAsync("api-key");

Console.WriteLine($"Stored version: {stored.Version}");
Console.WriteLine(retrieved?.Value);
```

### Key Vault behavior that the in-memory provider guarantees

The in-memory Key Vault provider keeps the current seam intentionally narrow:

- Missing secret lookups return `null` instead of throwing a synthetic not-found exception.
- Secret names are treated case-insensitively by the in-memory provider.
- Setting the same logical secret name again replaces the latest value and produces a fresh `Version`.
- The returned `AzureKeyVaultSecretRecord` includes `Name`, `Value`, and the latest `Version` when available.

## Test-only inspection with `InMemoryKeyVaultState`

`InMemoryKeyVaultState` is a **test-only inspection surface** that `AddAzureKeyVaultInMemory()` registers for observability. Production-facing code should depend on `IAzureKeyVaultFactory` instead.

```csharp
using Forgate.Azure.InMemory.KeyVault.InMemory;

var state = provider.GetRequiredService<InMemoryKeyVaultState>();

var secretExists = state.SecretExists("api-key");
var logicalSecretNames = state.SecretNames;
```

## When to use the SDK-backed Key Vault registration instead

When your host should talk to real Azure Key Vault, register the SDK-backed mode instead:

```csharp
services.AddAzureKeyVaultSdk();
```

That registration expects the host to have already registered `SecretClient` in DI. The consuming application code can keep the same `IAzureKeyVaultFactory` seam while the backend changes.

## Mixing backends across resources

Each resource chooses its backend independently, so this is valid:

```csharp
using Forgate.Azure.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddAzureServiceBusInMemory();
services.AddAzureBlobSdk();
services.AddAzureKeyVaultInMemory();
```

What is not valid is registering both backends for the **same** resource in the same service collection, for example both `AddAzureBlobSdk()` and `AddAzureBlobInMemory()`.

## Scope of this guide

This guide intentionally stays at the package boundary:

- it shows the real public namespaces and DI registration methods,
- it documents the deterministic in-memory behavior that the current tests prove for Service Bus, Blob Storage, and Key Vault,
- it shows how to switch each resource independently between in-memory and SDK-backed modes,
- and it avoids repo-only setup assumptions.

Fresh consumer-project restore/use proof through a local package feed remains a separate acceptance step.
