---
id: S02
parent: M001
milestone: M001
provides:
  - Truthful in-memory Service Bus queue ingress that requires explicit queue creation and preserves message body plus metadata in pending state.
  - Topology-aware topic publish fan-out into canonical `<topic>/Subscriptions/<subscription>` pending paths with independent cloned envelopes per subscription.
  - Dedicated ingress tests and shared-state inspection surfaces that make Service Bus topology and pending messages observable inside the `dotnet test` loop.
requires:
  - slice: S01
    provides: The explicit Service Bus factory seam, shared `InMemoryServiceBusState` root, and queue/topic/subscription administration contracts introduced in S01.
affects:
  - S03
key_files:
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - .gsd/DECISIONS.md
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
key_decisions:
  - Require queue or topic topology to be declared before in-memory enqueue operations mutate pending state.
  - Represent topic publishes as fan-out into declared `<topic>/Subscriptions/<subscription>` pending queues and never as pending messages stored on the topic name itself.
  - Keep `IAzureServiceBusFactory` and the sender surface thin over `InMemoryServiceBusState`, leaving `StartProcessingAsync()` as the explicit S03 boundary.
patterns_established:
  - Make Service Bus topology explicit in tests: call the in-memory administration API first, then assert against shared-state pending buffers rather than letting sender operations invent topology.
  - Model topic publishes as writes to canonical subscription entity paths (`<topic>/Subscriptions/<subscription>`) and treat the topic name itself as a routing key, not as a pending-message queue.
  - Keep ingress behavior centralized in `InMemoryServiceBusState` so the sender remains a thin transport wrapper and later processor work can consume the same observable pending-envelope surface.
observability_surfaces:
  - `InMemoryServiceBusState.QueueExists(...)`, `TopicExists(...)`, and `SubscriptionExists(...)` let tests assert declared topology before and after ingress.
  - `InMemoryServiceBusState.GetPendingMessages(entityPath)` now exposes truthful pending envelopes for declared queues and canonical subscription entity paths while leaving `GetPendingMessages(topicName)` empty after publish.
  - Stored `InMemoryServiceBusEnvelope` records preserve cloned body bytes, `MessageId`, and application properties so downstream processor tests can assert against stable ingress inputs.
  - Undeclared queue/topic sends now surface actionable `InvalidOperationException` diagnostics that name the entity path and reference the correct administration creation method.
drill_down_paths:
  - .gsd/milestones/M001/slices/S02/tasks/T01-SUMMARY.md
  - .gsd/milestones/M001/slices/S02/tasks/T02-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-30T21:42:28.922Z
blocker_discovered: false
---

# S02: In-memory Service Bus topology and message ingress

**Verified topology-aware in-memory Service Bus ingress: declared queues store preserved pending envelopes, declared topics fan out cloned envelopes into subscription paths, and undeclared topology fails loudly inside `dotnet test`.**

## What Happened

S02 turned the Service Bus in-memory seam from registration-plus-buffering into a truthful ingress surface that respects declared topology. The slice added dedicated tests under `tests/Azure.InMemory.Tests/ServiceBus/InMemory/` so Service Bus ingress no longer depends on DI smoke coverage. Those tests now prove that queue sends only work after `IAzureServiceBusAdministration.CreateQueueAsync(...)`, that the shared state exposes exactly one pending envelope after a single send, and that the stored envelope preserves the caller body, `MessageId`, and application properties that S03 will need during processor execution.

The slice also closed the roadmap demo for topic ingress. `InMemoryServiceBusState` now distinguishes queue sends from topic publishes: when the entity path matches a declared queue, the message is appended to that queue’s pending buffer; when it matches a declared topic, the state fans the message out into each declared subscription entity path using the canonical `<topic>/Subscriptions/<subscription>` format. Each subscription receives its own cloned `InMemoryServiceBusEnvelope`, including its own application-properties dictionary, while `GetPendingMessages(topicName)` remains empty so the in-memory shape stays aligned with the downstream processor model instead of inventing a fake topic-local queue.

Negative paths were made explicit rather than permissive. Sending to an undeclared queue or publishing to an undeclared topic now throws actionable `InvalidOperationException` messages that name the missing entity and point the caller to the corresponding administration creation methods. Blank queue, topic, and subscription names still fail fast through argument validation. The sender surface stayed thin over state, and the existing `StartProcessingAsync()` `NotSupportedException` boundary was kept intact so the slice delivers ingress truth without pretending processor execution exists before S03.

### Operational Readiness
- **Health signal:** `QueueExists(...)`, `TopicExists(...)`, `SubscriptionExists(...)`, and `GetPendingMessages(...)` expose the topology and pending-message state tests need to confirm queue ingress and topic fan-out behavior. A green `dotnet test ./Azure.InMemory.sln` run is the authoritative slice-level health check.
- **Failure signal:** undeclared topology now fails loudly with `InvalidOperationException` messages that name the entity path and direct the caller to `CreateQueueAsync(...)` or `CreateTopicAsync(...)`; processor startup still fails with a deliberate `NotSupportedException`, which is the expected pre-S03 boundary rather than a hidden runtime bug.
- **Recovery procedure:** create the queue, topic, and any subscriptions through `factory.Administration` before sending or publishing, then re-run the focused ingress tests or full solution tests. If processing behavior is needed, stop and move to S03 rather than layering ad hoc execution into the ingress slice.
- **Monitoring gaps:** there are still no settlement, retry, error, dead-letter, or delivery-count signals. Tests can observe only topology and pending envelopes until S03 extends the harness.

## Verification

Executed every verification command named in the slice plan using the explicit relative solution path `./Azure.InMemory.sln`, all sequentially from this worktree: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.Queue` (✅ pass, 4 tests, 3719ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownQueue` (✅ pass, 1 test, 3214ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests` (✅ pass, 17 tests, 3160ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` (✅ pass, 27 tests, 3272ms), and `dotnet test ./Azure.InMemory.sln` (✅ pass, 37 tests, 3212ms). The passing suite proves declared queue ingress, topic publish fan-out into subscription paths, preserved envelope body/`MessageId`/application properties, actionable failures for undeclared topology, and the unchanged processor boundary.

## Requirements Advanced

- R003 — Preserved the infrastructure-free `dotnet test` loop while adding real Service Bus ingress behavior and verifying it through focused, Service Bus-wide, and full-solution test runs with no external infrastructure.
- R006 — Preserved the explicit `StartProcessingAsync()` boundary while ensuring pending queue and subscription paths now retain the body, `MessageId`, and application properties that S03 processor execution and settlement observability will consume.

## Requirements Validated

- R005 — `InMemoryServiceBusIngressTests` plus the broader `ServiceBus` and full-solution test runs prove a test can create queues/topics/subscriptions in memory, send or publish messages into them, and observe preserved pending envelopes on the correct queue or canonical subscription paths entirely inside `dotnet test`.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

- `InMemoryServiceBusProcessor.StartProcessingAsync()` still throws an explanatory `NotSupportedException`; this slice proves ingress only, not message consumption or settlement.
- Observability currently stops at topology existence plus pending-envelope inspection; there are still no completed, dead-lettered, errored, retry, or delivery-count signals.
- The in-memory Service Bus behavior remains intentionally basic and does not attempt advanced Azure Service Bus fidelity such as retries, sessions, or delivery semantics beyond truthful ingress routing.

## Follow-ups

- S03 should consume the existing pending queue and canonical subscription entity paths to implement truthful processor execution, completion, dead-letter, pending, and error observability without changing the public factory seam.
- S03 should keep treating `StartProcessingAsync()` as the handoff point from ingress proof to actual processing behavior, replacing the current explanatory `NotSupportedException` with real execution only when settlement observability is ready.
- If later slices add deeper Service Bus fidelity, keep the invariant that the topic name itself never becomes a pending-message queue and that each subscription receives an independent envelope copy.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — Added focused queue and topic ingress coverage that proves declared topology, preserved envelope metadata, actionable failures for undeclared topology, blank-name guards, and the unchanged processor boundary.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — Made queue sends and topic publishes topology-aware by rejecting undeclared entity paths, faning out topic publishes into canonical subscription entity paths, and cloning envelope/application-property payloads per subscription.
- `.gsd/KNOWLEDGE.md` — Recorded the canonical subscription-path observability pattern future Service Bus slices should assert against.
- `.gsd/PROJECT.md` — Refreshed the project snapshot to reflect that S02 is complete and that Service Bus ingress is now topology-aware while processor execution remains deferred to S03.
- `.gsd/DECISIONS.md` — Recorded the requirement-status decision that R005 is now validated by the slice evidence.
