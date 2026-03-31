---
id: T03
parent: S01
milestone: M002
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs", "src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs", ".gsd/DECISIONS.md", ".gsd/milestones/M002/slices/S01/tasks/T03-SUMMARY.md"]
key_decisions: ["Expose DeliveryCount on AzureServiceBusReceivedMessageContext, keep delivery-count/max-delivery metadata on the state-owned envelope lifecycle, and record each failed queue attempt in the errored bucket even when the message is requeued or auto-dead-lettered."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Passed the task’s exact verification bar with DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue and DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests. The focused redelivery suite passed 4 tests and the processor suite passed 12 tests."
completed_at: 2026-03-31T02:43:06.709Z
blocker_discovered: false
---

# T03: Added queue redelivery and max-delivery dead-letter fidelity to the in-memory Service Bus processor while exposing delivery-count metadata through the existing test harness.

> Added queue redelivery and max-delivery dead-letter fidelity to the in-memory Service Bus processor while exposing delivery-count metadata through the existing test harness.

## What Happened
---
id: T03
parent: S01
milestone: M002
key_files:
  - src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs
  - .gsd/DECISIONS.md
  - .gsd/milestones/M002/slices/S01/tasks/T03-SUMMARY.md
key_decisions:
  - Expose DeliveryCount on AzureServiceBusReceivedMessageContext, keep delivery-count/max-delivery metadata on the state-owned envelope lifecycle, and record each failed queue attempt in the errored bucket even when the message is requeued or auto-dead-lettered.
duration: ""
verification_result: passed
completed_at: 2026-03-31T02:43:06.711Z
blocker_discovered: false
---

# T03: Added queue redelivery and max-delivery dead-letter fidelity to the in-memory Service Bus processor while exposing delivery-count metadata through the existing test harness.

**Added queue redelivery and max-delivery dead-letter fidelity to the in-memory Service Bus processor while exposing delivery-count metadata through the existing test harness.**

## What Happened

Extended the shared Service Bus seam so received-message contexts now expose DeliveryCount and processor options can carry MaxDeliveryCount with fail-fast validation. InMemoryServiceBusState now persists DeliveryCount and MaxDeliveryCount on pending, completed, dead-lettered, and errored lifecycle records. InMemoryServiceBusFactory now treats failed queue processing as a deterministic state transition: it records the failed attempt in errored outcomes, requeues only that queue envelope for the next explicit StartProcessingAsync() run with DeliveryCount + 1, and dead-letters it with an exhaustion reason once the configured maximum is reached. Added a dedicated InMemoryServiceBusRedeliveryTests suite and updated the existing queue processor exception test to reflect the new contract. Also updated the SDK-backed factory so DeliveryCount stays truthful and compile-compatible across both backends.

## Verification

Passed the task’s exact verification bar with DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue and DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests. The focused redelivery suite passed 4 tests and the processor suite passed 12 tests.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue` | 0 | ✅ pass | 3550ms |
| 2 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests` | 0 | ✅ pass | 3456ms |


## Deviations

Updated src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs and src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs in addition to the planned in-memory files so the new DeliveryCount and MaxDeliveryCount seam additions remained compile-compatible and truthful across both backends.

## Known Issues

None.

## Files Created/Modified

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
- `.gsd/DECISIONS.md`
- `.gsd/milestones/M002/slices/S01/tasks/T03-SUMMARY.md`


## Deviations
Updated src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs and src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs in addition to the planned in-memory files so the new DeliveryCount and MaxDeliveryCount seam additions remained compile-compatible and truthful across both backends.

## Known Issues
None.
