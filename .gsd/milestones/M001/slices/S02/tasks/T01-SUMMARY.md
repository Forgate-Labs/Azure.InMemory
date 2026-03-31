---
id: T01
parent: S02
milestone: M001
provides: []
requires: []
affects: []
key_files: ["tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs", "src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs", ".gsd/milestones/M001/slices/S02/tasks/T01-SUMMARY.md"]
key_decisions: ["D015: Require declared Service Bus topology before in-memory enqueue operations mutate pending state."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Passed the focused queue-ingress checks, the full ingress test class, the broader Service Bus regression filter, and the full solution test loop via explicit ./Azure.InMemory.sln commands. The new tests directly verified QueueExists(...), GetPendingMessages(queueName), preserved envelope body/MessageId/application properties, and actionable InvalidOperationException text for undeclared queue ingress."
completed_at: 2026-03-30T21:32:21.061Z
blocker_discovered: false
---

# T01: Added topology-gated in-memory queue ingress tests and rejected undeclared queue sends.

> Added topology-gated in-memory queue ingress tests and rejected undeclared queue sends.

## What Happened
---
id: T01
parent: S02
milestone: M001
key_files:
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - .gsd/milestones/M001/slices/S02/tasks/T01-SUMMARY.md
key_decisions:
  - D015: Require declared Service Bus topology before in-memory enqueue operations mutate pending state.
duration: ""
verification_result: passed
completed_at: 2026-03-30T21:32:21.063Z
blocker_discovered: false
---

# T01: Added topology-gated in-memory queue ingress tests and rejected undeclared queue sends.

**Added topology-gated in-memory queue ingress tests and rejected undeclared queue sends.**

## What Happened

Created dedicated in-memory Service Bus ingress coverage under tests/Azure.InMemory.Tests/ServiceBus/InMemory and used DI-resolved InMemoryServiceBusState assertions to prove queue topology must be declared before send. Added positive coverage for queue creation plus one pending envelope with preserved body, MessageId, and application properties, and negative coverage for blank queue names and undeclared queue sends. Updated InMemoryServiceBusState so enqueue operations now reject undeclared entity paths with actionable InvalidOperationException text instead of silently creating pending buffers; no factory change was needed because the sender remains thin over state.

## Verification

Passed the focused queue-ingress checks, the full ingress test class, the broader Service Bus regression filter, and the full solution test loop via explicit ./Azure.InMemory.sln commands. The new tests directly verified QueueExists(...), GetPendingMessages(queueName), preserved envelope body/MessageId/application properties, and actionable InvalidOperationException text for undeclared queue ingress.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.Queue` | 0 | ✅ pass | 3506ms |
| 2 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownQueue` | 0 | ✅ pass | 3546ms |
| 3 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests` | 0 | ✅ pass | 4507ms |
| 4 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` | 0 | ✅ pass | 3498ms |
| 5 | `dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 3289ms |


## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `.gsd/milestones/M001/slices/S02/tasks/T01-SUMMARY.md`


## Deviations
None.

## Known Issues
None.
