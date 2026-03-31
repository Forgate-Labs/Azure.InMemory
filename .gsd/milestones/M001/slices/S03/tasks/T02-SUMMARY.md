---
id: T02
parent: S03
milestone: M001
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs", "tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Handler exceptions and invalid second-settlement attempts now produce terminal errored outcomes in state instead of being implicitly requeued, while successful-but-unsettled handlers remain the only path that stays pending.", "Queue and subscription processors now share the same in-memory lifecycle engine and consume canonical subscription entity paths like <topic>/Subscriptions/<subscription> instead of inventing a topic-local processing buffer."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran the focused processor suite, the broader Service Bus suite, and the full solution sequentially from ./Azure.InMemory.sln. The processor suite proved queue completion/pending/auto-complete behavior still worked while new subscription dead-letter and error paths recorded inspectable outcomes on canonical entity paths. The broader Service Bus suite and full solution then passed unchanged, confirming no regressions across ingress, DI, Blob, or Key Vault seams."
completed_at: 2026-03-30T22:08:46.112Z
blocker_discovered: false
---

# T02: Added canonical subscription processing plus inspectable dead-letter and error outcomes for in-memory Service Bus processors.

> Added canonical subscription processing plus inspectable dead-letter and error outcomes for in-memory Service Bus processors.

## What Happened
---
id: T02
parent: S03
milestone: M001
key_files:
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Handler exceptions and invalid second-settlement attempts now produce terminal errored outcomes in state instead of being implicitly requeued, while successful-but-unsettled handlers remain the only path that stays pending.
  - Queue and subscription processors now share the same in-memory lifecycle engine and consume canonical subscription entity paths like <topic>/Subscriptions/<subscription> instead of inventing a topic-local processing buffer.
duration: ""
verification_result: passed
completed_at: 2026-03-30T22:08:46.114Z
blocker_discovered: false
---

# T02: Added canonical subscription processing plus inspectable dead-letter and error outcomes for in-memory Service Bus processors.

**Added canonical subscription processing plus inspectable dead-letter and error outcomes for in-memory Service Bus processors.**

## What Happened

Extended InMemoryServiceBusState with dead-lettered and errored outcome stores keyed by queue or canonical subscription entity path, preserving body, message id, cloned application properties, enqueue time, dead-letter reason, and captured exception for later assertions. Reworked InMemoryServiceBusFactory so queue and subscription processors share one lifecycle implementation: startup now validates declared queue or subscription paths, subscription processors drain <topic>/Subscriptions/<subscription> directly, DeadLetterMessageAsync() stores a truthful dead-letter outcome with preserved reason text, and handler or settlement failures are recorded as terminal errored outcomes while still invoking ProcessErrorAsync when configured. Expanded InMemoryServiceBusProcessorTests to cover undeclared subscription startup, subscription dead-lettering, handler exceptions with and without ProcessErrorAsync, and invalid settlement ordering without duplicating terminal outcomes, then removed the stale ingress test that still expected subscription processing to remain deferred.

## Verification

Ran the focused processor suite, the broader Service Bus suite, and the full solution sequentially from ./Azure.InMemory.sln. The processor suite proved queue completion/pending/auto-complete behavior still worked while new subscription dead-letter and error paths recorded inspectable outcomes on canonical entity paths. The broader Service Bus suite and full solution then passed unchanged, confirming no regressions across ingress, DI, Blob, or Key Vault seams.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests` | 0 | ✅ pass | 3329ms |
| 2 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` | 0 | ✅ pass | 3242ms |
| 3 | `dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 3297ms |


## Deviations

Adjusted the stale S02 ingress test that asserted subscription processing was still deferred, because the slice contract for T02 replaces that boundary with real canonical subscription execution.

## Known Issues

None.

## Files Created/Modified

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
Adjusted the stale S02 ingress test that asserted subscription processing was still deferred, because the slice contract for T02 replaces that boundary with real canonical subscription execution.

## Known Issues
None.
