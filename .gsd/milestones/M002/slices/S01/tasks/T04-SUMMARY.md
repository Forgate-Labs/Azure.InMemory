---
id: T04
parent: S01
milestone: M002
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Reuse the same state-owned requeue/dead-letter bookkeeping for queues and canonical subscription paths.", "Keep redelivery isolation assertions keyed to canonical entity paths like <topic>/Subscriptions/<subscription> rather than the topic name."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran the exact task verification sequence from ./Azure.InMemory.sln: DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests (6 passed), DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus (44 passed), and DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln (74 passed). The focused redelivery coverage now proves canonical subscription retry/dead-letter isolation, and the broader Service Bus plus full-solution runs stayed green."
completed_at: 2026-03-31T02:48:59.175Z
blocker_discovered: false
---

# T04: Extended in-memory Service Bus redelivery fidelity to canonical subscription paths and closed the slice with full regression proof.

> Extended in-memory Service Bus redelivery fidelity to canonical subscription paths and closed the slice with full regression proof.

## What Happened
---
id: T04
parent: S01
milestone: M002
key_files:
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Reuse the same state-owned requeue/dead-letter bookkeeping for queues and canonical subscription paths.
  - Keep redelivery isolation assertions keyed to canonical entity paths like <topic>/Subscriptions/<subscription> rather than the topic name.
duration: ""
verification_result: passed
completed_at: 2026-03-31T02:48:59.176Z
blocker_discovered: false
---

# T04: Extended in-memory Service Bus redelivery fidelity to canonical subscription paths and closed the slice with full regression proof.

**Extended in-memory Service Bus redelivery fidelity to canonical subscription paths and closed the slice with full regression proof.**

## What Happened

Removed the subscription-only failure short-circuit in InMemoryServiceBusFactory so failed canonical subscription deliveries now requeue for the next explicit StartProcessingAsync() run, increment DeliveryCount on the same shared state-owned lifecycle, and dead-letter when MaxDeliveryCount is exhausted. Added subscription redelivery coverage proving that a failing <topic>/Subscriptions/<subscription> path progresses independently while sibling subscription clones remain deliverable, updated the subscription processor regression to replace the old terminal-error assumption with ProcessErrorAsync plus next-run redelivery, tightened ingress assertions to preserve canonical routing while checking default delivery metadata, and recorded the entity-path isolation invariant in .gsd/KNOWLEDGE.md.

## Verification

Ran the exact task verification sequence from ./Azure.InMemory.sln: DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests (6 passed), DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus (44 passed), and DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln (74 passed). The focused redelivery coverage now proves canonical subscription retry/dead-letter isolation, and the broader Service Bus plus full-solution runs stayed green.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests` | 0 | ✅ pass | 4233ms |
| 2 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` | 0 | ✅ pass | 3412ms |
| 3 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 3416ms |


## Deviations

The task plan listed InMemoryServiceBusState as a likely implementation file, but T03 had already centralized queue/subscription delivery metadata there. The actual product-code change for T04 was concentrated in InMemoryServiceBusFactory, with the rest of the work in regression tests and the knowledge log.

## Known Issues

None.

## Files Created/Modified

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
The task plan listed InMemoryServiceBusState as a likely implementation file, but T03 had already centralized queue/subscription delivery metadata there. The actual product-code change for T04 was concentrated in InMemoryServiceBusFactory, with the rest of the work in regression tests and the knowledge log.

## Known Issues
None.
