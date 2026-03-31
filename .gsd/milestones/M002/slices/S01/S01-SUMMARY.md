---
id: S01
parent: M002
milestone: M002
provides:
  - An authoritative M002 root checkout with `./Azure.InMemory.sln`, `src/`, and `tests/` restored and verified as the execution target for the remaining milestone work.
  - Deterministic queue and canonical-subscription redelivery behavior that requeues only on the next explicit processor run, increments `DeliveryCount` once per failure, and dead-letters automatically at the configured `MaxDeliveryCount`.
  - A richer Service Bus observability surface that preserves `DeliveryCount`/`MaxDeliveryCount` in pending, dead-lettered, and errored outcomes so downstream packaging and consumer slices can assert retry fidelity without new seams.
requires:
  []
affects:
  - S02
  - S03
key_files:
  - Azure.InMemory.sln
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - .gsd/DECISIONS.md
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
key_decisions:
  - Restore and verify the authoritative solution checkout at the active M002 root before executing any fidelity or packaging work; later verification must target `./Azure.InMemory.sln`, not a sibling worktree snapshot.
  - Keep redelivery as a state-owned lifecycle extension over repeated explicit `StartProcessingAsync()` runs, with `DeliveryCount` surfaced to handlers and preserved in pending/dead-letter/errored inspection state instead of adding background retry abstractions.
  - Reuse the same retry/dead-letter bookkeeping on canonical `<topic>/Subscriptions/<subscription>` entity paths so each subscription clone advances and exhausts independently from its siblings.
patterns_established:
  - Keep deeper Service Bus fidelity in `InMemoryServiceBusState` and keep `InMemoryServiceBusFactory` thin over state-owned lifecycle transitions.
  - Model retries as explicit next-run redelivery in the existing deterministic processor loop rather than introducing background polling or hidden timers.
  - Apply delivery bookkeeping per canonical entity path so topic fan-out clones remain isolated and downstream tests can assert exact counts on one subscription without mutating siblings.
observability_surfaces:
  - `InMemoryServiceBusState.GetPendingMessages(entityPath)` now exposes incremented `DeliveryCount` and configured `MaxDeliveryCount` after a failed attempt awaits the next explicit processor run.
  - `InMemoryServiceBusState.GetDeadLetteredMessages(entityPath)` exposes max-delivery exhaustion outcomes with preserved envelope metadata plus a dead-letter reason that names the exhausted message and counts.
  - `InMemoryServiceBusState.GetErroredMessages(entityPath)` records each failed attempt even when the envelope is subsequently requeued or automatically dead-lettered, making retry history inspectable for tests.
  - `AzureServiceBusReceivedMessageContext.DeliveryCount` gives handlers and tests direct visibility into which redelivery attempt is currently being processed without widening the public factory seam.
drill_down_paths:
  - .gsd/milestones/M002/slices/S01/tasks/T01-SUMMARY.md
  - .gsd/milestones/M002/slices/S01/tasks/T02-SUMMARY.md
  - .gsd/milestones/M002/slices/S01/tasks/T03-SUMMARY.md
  - .gsd/milestones/M002/slices/S01/tasks/T04-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-31T02:55:42.762Z
blocker_discovered: false
---

# S01: Observable Service Bus redelivery fidelity

**Verified the restored M002 root checkout and delivered deterministic queue/subscription redelivery with observable delivery-count progression and max-delivery dead-letter behavior through the existing in-memory Service Bus seam.**

## What Happened

S01 had two concrete outcomes. First, T02 repaired the execution precondition by restoring the authoritative solution checkout into the active M002 root and proving `./Azure.InMemory.sln` resolves from this worktree instead of a sibling `.gsd/worktrees/M001` snapshot. That matters to the remaining milestone because packaging and external-consumer proof now have a trustworthy producer root to build from.

Second, T03 and T04 deepened the existing Service Bus seam without rewriting it. `InMemoryServiceBusState` now keeps `DeliveryCount` and `MaxDeliveryCount` on the state-owned envelope lifecycle, `AzureServiceBusReceivedMessageContext` surfaces the current `DeliveryCount` to handlers, and `InMemoryServiceBusFactory` still drains one explicit `StartProcessingAsync()` batch at a time. The changed contract is failure handling: when a handler throws before settlement, the state records an errored attempt, requeues only that envelope for the next explicit run with `DeliveryCount + 1`, and automatically dead-letters it once the configured maximum is exhausted. This same lifecycle now applies to canonical subscription paths such as `orders/Subscriptions/billing`, so one failing subscription clone can retry and exhaust independently while sibling subscriptions keep their untouched pending copy and default delivery metadata until their own processor runs.

The focused redelivery suite now proves the slice demo for both queues and canonical subscriptions: the first failure makes the next explicit processor run observe delivery count 2, max-delivery exhaustion moves the message into dead-letter with an exhaustion reason, no duplicate pending copy survives the final failure, and failed attempts remain inspectable through `GetErroredMessages(entityPath)` even when the message is later requeued or dead-lettered. Existing processor and ingress suites were updated only where the redelivery contract genuinely changed, so the public DI/factory seam, synchronous in-process processor model, and canonical subscription path rules from M001 remain intact.

### Operational Readiness
- **Health signal:** a green sequential `dotnet test ./Azure.InMemory.sln` run is the authoritative slice-level health check, while `GetPendingMessages(entityPath)`, `GetDeadLetteredMessages(entityPath)`, and `GetErroredMessages(entityPath)` now expose the current retry/dead-letter lifecycle together with `DeliveryCount` and `MaxDeliveryCount`.
- **Failure signal:** undeclared queues/subscriptions still fail fast with actionable diagnostics; handler exceptions now leave durable errored outcomes per attempt, and max-delivery exhaustion is visible through a dead-letter reason that names the exhausted message and counts.
- **Recovery procedure:** declare the queue/topic/subscription through `factory.Administration`, rerun the processor explicitly with `StartProcessingAsync()`, and inspect the state-backed pending/dead-letter/errored buckets on the canonical entity path to see whether the message was retried, exhausted, or completed.
- **Monitoring gaps:** the in-memory model still intentionally omits background polling, lock renewal, sessions, scheduling, and other deeper Azure Service Bus semantics, so retry truth is limited to the explicit per-run lifecycle proven in this slice.

## Verification

Executed the slice verification plan sequentially from this worktree using the explicit relative solution path `./Azure.InMemory.sln`: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests` (✅ pass, 6 tests, 3431 ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` (✅ pass, 44 tests, 3453 ms), and `dotnet test ./Azure.InMemory.sln` (✅ pass, 74 tests, 3443 ms). These runs prove the focused redelivery suite, the broader Service Bus regression surface, and the full infrastructure-free solution loop all stay green after queue and canonical-subscription redelivery fidelity landed.

## Requirements Advanced

- R020 — Delivered the first deferred advanced Service Bus fidelity slice by adding observable queue and canonical-subscription redelivery, delivery-count progression, and max-delivery dead-letter behavior while preserving the deterministic in-process seam.

## Requirements Validated

None.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

S01 raises Service Bus fidelity only in the narrow redelivery area. The in-memory processor still runs synchronously over an explicit snapshot per `StartProcessingAsync()` call and still omits background retry loops, lock renewal, sessions, scheduling, duplicate detection, and broader Azure Service Bus parity work that remains deferred beyond this slice.

## Follow-ups

S02 and S03 should treat the active M002 root checkout as the authoritative producer tree and keep verification commands pointed at `./Azure.InMemory.sln`. Packaging docs and external-consumer proof should preserve the now-proven canonical subscription-path and explicit rerun semantics instead of implying background retries or topic-local processor state.

## Files Created/Modified

- `Azure.InMemory.sln` — Restored the solution into the active M002 root so all later verification and packaging work can target `./Azure.InMemory.sln` from this worktree.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — Added state-owned delivery-count/max-delivery metadata, redelivery helpers, and pending/dead-letter/errored inspection fidelity for retry progression on queues and canonical subscription paths.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — Updated processor failure handling so unsettled handler exceptions requeue only for the next explicit run, surface `DeliveryCount` to handlers, and dead-letter automatically when the configured max is exhausted.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` — Added focused queue and canonical-subscription redelivery coverage for delivery-count progression, retry isolation, max-delivery exhaustion, and invalid max-delivery configuration.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — Adjusted processor expectations to the new redelivery contract, including inspectable failed attempts, explicit rerun semantics, and preserved counts on completed outcomes after retry.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — Kept ingress assertions aligned with the richer envelope metadata so pending queue and subscription messages still preserve body, properties, and initial delivery metadata.
- `.gsd/DECISIONS.md` — Recorded the subscription redelivery decision so downstream slices know canonical subscription paths reuse the same independent retry/dead-letter lifecycle.
- `.gsd/KNOWLEDGE.md` — Captured the non-obvious rule that processor-specific `MaxDeliveryCount` is applied per canonical entity path and that untouched sibling subscription clones retain their own pending copy and delivery metadata until processed.
- `.gsd/PROJECT.md` — Updated the project snapshot to reflect that M002/S01 is complete and that the next milestone work is packaging and external consumer proof.
