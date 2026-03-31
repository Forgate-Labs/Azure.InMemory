---
id: T02
parent: S01
milestone: M001
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs", "src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs", "src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs", "tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Resolved SDK-backed factories through DI activation lambdas so missing ServiceBusClient and ServiceBusAdministrationClient dependencies fail with actionable InvalidOperationException messages at factory resolution time.", "Used a resource-specific registration marker plus TryAddSingleton registrations so repeated same-mode calls are idempotent while mixed Service Bus backend registration fails fast."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran the targeted Service Bus registration suite and the narrower conflicting-registration probe from the task plan. Both passed, proving that the same IAzureServiceBusFactory abstraction resolves to the selected backend, that in-memory state stays singleton-scoped, and that missing/duplicate registration paths now surface explicit actionable errors."
completed_at: 2026-03-30T20:49:07.704Z
blocker_discovered: false
---

# T02: Added Service Bus SDK/in-memory registrations with fail-fast DI guards and shared in-memory state resolution.

> Added Service Bus SDK/in-memory registrations with fail-fast DI guards and shared in-memory state resolution.

## What Happened
---
id: T02
parent: S01
milestone: M001
key_files:
  - src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs
  - src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs
  - src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Resolved SDK-backed factories through DI activation lambdas so missing ServiceBusClient and ServiceBusAdministrationClient dependencies fail with actionable InvalidOperationException messages at factory resolution time.
  - Used a resource-specific registration marker plus TryAddSingleton registrations so repeated same-mode calls are idempotent while mixed Service Bus backend registration fails fast.
duration: ""
verification_result: passed
completed_at: 2026-03-30T20:49:07.706Z
blocker_discovered: false
---

# T02: Added Service Bus SDK/in-memory registrations with fail-fast DI guards and shared in-memory state resolution.

**Added Service Bus SDK/in-memory registrations with fail-fast DI guards and shared in-memory state resolution.**

## What Happened

Implemented the Service Bus registration seam end-to-end. I refined IAzureServiceBusFactory by adding a small library-owned sender options record and IAsyncDisposable sender lifecycle so the seam stays focused without returning raw Azure SDK clients, while still supporting sender, processor, and administration creation. I added AzureServiceBusSdkFactory as a thin adapter over DI-registered ServiceBusClient and ServiceBusAdministrationClient, including actionable failure text when either SDK dependency is missing. I also added InMemoryServiceBusFactory plus a singleton InMemoryServiceBusState root that tracks queues, topics, subscriptions, and pending envelopes for later slices, and wired AddAzureServiceBusSdk() / AddAzureServiceBusInMemory() through explicit same-resource conflict guards. Finally, I replaced the placeholder Service Bus registration test with focused coverage for SDK selection, in-memory selection, shared singleton-state reuse, missing-client failures, and conflicting-registration behavior.

## Verification

Ran the targeted Service Bus registration suite and the narrower conflicting-registration probe from the task plan. Both passed, proving that the same IAzureServiceBusFactory abstraction resolves to the selected backend, that in-memory state stays singleton-scoped, and that missing/duplicate registration paths now surface explicit actionable errors.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests` | 0 | ✅ pass | 3391ms |
| 2 | `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests.Conflicting` | 0 | ✅ pass | 3351ms |


## Deviations

Refined the Service Bus public seam slightly beyond the planner snapshot by adding AzureServiceBusSenderOptions and making IAzureServiceBusSender disposable so sender creation can carry a small amount of library-owned configuration and preserve underlying SDK sender lifetime semantics.

## Known Issues

InMemoryServiceBusFactory now supports truthful registration, sender buffering, and topology state, but StartProcessingAsync() on the in-memory processor still throws an explanatory NotSupportedException until later slices add real in-memory processor behavior. Blob, Key Vault, and mixed-resource composition tests remain placeholder work for T03-T05.

## Files Created/Modified

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs`
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
Refined the Service Bus public seam slightly beyond the planner snapshot by adding AzureServiceBusSenderOptions and making IAzureServiceBusSender disposable so sender creation can carry a small amount of library-owned configuration and preserve underlying SDK sender lifetime semantics.

## Known Issues
InMemoryServiceBusFactory now supports truthful registration, sender buffering, and topology state, but StartProcessingAsync() on the in-memory processor still throws an explanatory NotSupportedException until later slices add real in-memory processor behavior. Blob, Key Vault, and mixed-resource composition tests remain placeholder work for T03-T05.
