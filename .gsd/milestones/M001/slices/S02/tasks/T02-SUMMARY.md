---
id: T02
parent: S02
milestone: M001
provides: []
requires: []
affects: []
key_files: ["tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs", ".gsd/milestones/M001/slices/S02/tasks/T02-SUMMARY.md"]
key_decisions: ["D016: Publish sends fan out cloned envelopes into declared `<topic>/Subscriptions/<subscription>` pending queues and never enqueue against the topic name itself."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Passed `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`, `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`, and `dotnet test ./Azure.InMemory.sln`. The focused ingress tests directly verified per-subscription pending-message counts, preserved body/MessageId/application properties, empty topic-path pending state after publish, explicit unknown-topic errors, and the unchanged deferred processor boundary."
completed_at: 2026-03-30T21:38:17.073Z
blocker_discovered: false
---

# T02: Added topology-aware topic publish fan-out so in-memory Service Bus enqueues cloned envelopes per subscription path and never on the topic name itself.

> Added topology-aware topic publish fan-out so in-memory Service Bus enqueues cloned envelopes per subscription path and never on the topic name itself.

## What Happened
---
id: T02
parent: S02
milestone: M001
key_files:
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - .gsd/milestones/M001/slices/S02/tasks/T02-SUMMARY.md
key_decisions:
  - D016: Publish sends fan out cloned envelopes into declared `<topic>/Subscriptions/<subscription>` pending queues and never enqueue against the topic name itself.
duration: ""
verification_result: passed
completed_at: 2026-03-30T21:38:17.075Z
blocker_discovered: false
---

# T02: Added topology-aware topic publish fan-out so in-memory Service Bus enqueues cloned envelopes per subscription path and never on the topic name itself.

**Added topology-aware topic publish fan-out so in-memory Service Bus enqueues cloned envelopes per subscription path and never on the topic name itself.**

## What Happened

Extended the in-memory Service Bus ingress test suite with topic/subscription publish coverage that asserts literal `<topic>/Subscriptions/<subscription>` entity paths, verifies batch fan-out counts and preserved message metadata, rejects undeclared topic publishes with actionable diagnostics, and confirms `StartProcessingAsync()` is still the explicit S03 `NotSupportedException` boundary. Updated `InMemoryServiceBusState.Enqueue(...)` so queue sends still require declared topology while topic publishes now fan out one cloned envelope per declared subscription path and never create a pending queue for the topic name itself. `InMemoryServiceBusFactory` remained thin because both single-message and batch sends already delegate through the shared state behavior.

## Verification

Passed `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`, `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`, and `dotnet test ./Azure.InMemory.sln`. The focused ingress tests directly verified per-subscription pending-message counts, preserved body/MessageId/application properties, empty topic-path pending state after publish, explicit unknown-topic errors, and the unchanged deferred processor boundary.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests` | 0 | ✅ pass | 3333ms |
| 2 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` | 0 | ✅ pass | 3252ms |
| 3 | `dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 3284ms |


## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `.gsd/milestones/M001/slices/S02/tasks/T02-SUMMARY.md`


## Deviations
None.

## Known Issues
None.
