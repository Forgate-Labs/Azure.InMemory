---
id: S03
parent: M001
milestone: M001
provides:
  - In-memory queue and subscription processors that consume the current pending batch and preserve deterministic per-run behavior.
  - Inspectable completed, dead-lettered, pending, and errored outcome surfaces keyed by queue or canonical `<topic>/Subscriptions/<subscription>` entity path.
  - Actionable diagnostics and tests for undeclared processors, auto-complete vs pending behavior, handler exceptions, and invalid settlement ordering.
requires:
  - slice: S02
    provides: Explicit Service Bus factory/admin seams, declared topology, canonical subscription entity paths, and pending envelope state that S03 processors now consume and settle.
affects:
  []
key_files:
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - .gsd/DECISIONS.md
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
key_decisions:
  - Drain the current pending batch synchronously and route settlement through `InMemoryServiceBusState` so processor behavior stays deterministic and inspectable in tests.
  - Treat handler exceptions and invalid second-settlement attempts as terminal errored outcomes, preserving any first successful completion or dead-letter transition and surfacing the failure through `ProcessErrorAsync` when configured.
  - Keep subscription processing bound to canonical `<topic>/Subscriptions/<subscription>` entity paths instead of inventing a topic-local processor buffer.
patterns_established:
  - Keep Service Bus lifecycle transitions centralized in `InMemoryServiceBusState` and keep the processor adapter thin over those state-owned APIs.
  - Drain a snapshot of the current pending batch during `StartProcessingAsync()` so queue and subscription tests can assert exact ordering and counts without racing new ingress.
  - Model processor failures as explicit errored outcomes rather than hidden requeues, while preserving any first successful completion or dead-letter outcome for the same envelope.
observability_surfaces:
  - `InMemoryServiceBusState.GetCompletedMessages(entityPath)` exposes completed envelopes with preserved body, `MessageId`, application properties, and entity path.
  - `InMemoryServiceBusState.GetDeadLetteredMessages(entityPath)` exposes dead-letter outcomes with preserved envelope metadata plus the stored dead-letter reason.
  - `InMemoryServiceBusState.GetErroredMessages(entityPath)` exposes terminal handler or settlement failures with the original envelope metadata and captured exception.
  - `InMemoryServiceBusState.GetPendingMessages(entityPath)` remains the source of truth for successfully-unsettled messages when `AutoCompleteMessages` is disabled, and undeclared processors fail with actionable diagnostics that name the missing queue or subscription creation method.
drill_down_paths:
  - .gsd/milestones/M001/slices/S03/tasks/T01-SUMMARY.md
  - .gsd/milestones/M001/slices/S03/tasks/T02-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-30T22:14:04.223Z
blocker_discovered: false
---

# S03: Processor execution and settlement observability

**Implemented deterministic in-memory Service Bus processor execution with inspectable completed, dead-lettered, pending, and errored outcomes for declared queues and canonical subscription paths.**

## What Happened

S03 closes the main Service Bus loop that earlier slices prepared. Building on S02's declared topology and pending-envelope state, the slice implements in-memory queue and subscription processors that consume the current pending batch, construct `AzureServiceBusReceivedMessageContext` from the stored envelope, and route lifecycle transitions back through `InMemoryServiceBusState`. Tests can now assert what actually happened to each message after processing instead of stopping at ingress proof.

The shared state now owns durable completed, dead-lettered, and errored outcome stores alongside the existing pending buffers. That keeps queue and subscription processing aligned with the canonical entity-path model from S02: queues process on the queue name, subscriptions process on `<topic>/Subscriptions/<subscription>`, and the topic path itself stays empty. The outcome records preserve the original body, `MessageId`, application properties, and, for dead-lettered or errored cases, the dead-letter reason or exception so downstream slices and tests can make strong assertions without depending on ambient handler state.

The slice also makes failure semantics explicit and deterministic. Successful handlers that do not settle leave messages pending unless `AutoCompleteMessages` is enabled, explicit completion moves the message into the completed bucket, dead-letter preserves the reason text on the canonical subscription path, and handler failures or invalid second-settlement attempts are recorded as terminal errored outcomes and forwarded through `ProcessErrorAsync` when configured. Together, that delivers the roadmap demo: an in-memory processor can now consume a published message and tests can prove whether it completed, dead-lettered, stayed pending, or errored, all inside `dotnet test`.

## Verification

Executed every verification command named in the slice plan sequentially from this worktree using the explicit relative solution path `./Azure.InMemory.sln`: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue` (✅ pass, 8 tests, 3398ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests` (✅ pass, 12 tests, 3886ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` (✅ pass, 38 tests, 3810ms), and `dotnet test ./Azure.InMemory.sln` (✅ pass, 48 tests, 3439ms). The focused and broader regressions prove queue completion, pending retention, auto-complete, canonical subscription dead-lettering, handler-error recording, invalid-settlement diagnostics, and full-solution stability without Azure, Docker, or any external infrastructure.

## Requirements Advanced

- R003 — S03 keeps the entire processor execution path inside the test process and proves it with processor-focused, Service Bus-wide, and full-solution `dotnet test` runs that require no Azure resources, Docker, or external emulators.
- R005 — S03 consumes the pending queue and canonical subscription envelopes introduced in S02 and proves they are now usable as real processor inputs for queue and subscription execution.

## Requirements Validated

- R006 — `InMemoryServiceBusProcessorTests` prove `CompleteMessageAsync` and `DeadLetterMessageAsync` move stored envelopes into inspectable completed and dead-lettered outcome stores on declared queue and canonical subscription entity paths, and the broader `ServiceBus` plus full-solution runs stay green.
- R007 — The slice exposes and verifies pending, completed, dead-lettered, and errored outcome inspection through `InMemoryServiceBusState`, including actionable undeclared-topology diagnostics and invalid-settlement behavior, entirely inside `dotnet test`.
- R010 — S03 extends the in-memory Service Bus harness with durable state-backed inspection APIs (`GetCompletedMessages`, `GetDeadLetteredMessages`, `GetErroredMessages`) and outcome records that go beyond the official SDK surface specifically for test verification.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

- `StartProcessingAsync()` currently drains only the current pending snapshot and then returns; it is not a background polling processor.
- The in-memory Service Bus implementation still intentionally omits advanced fidelity such as retries, delivery counts, lock renewal, sessions, and other deeper Azure Service Bus semantics deferred under R020.
- Observability is state-backed and test-oriented rather than SDK-parity-focused, which is correct for M001 but means later fidelity work must keep these harness contracts truthful.

## Follow-ups

- Preserve the current canonical `<topic>/Subscriptions/<subscription>` processing path and state-owned observability surface if later milestones add deeper Service Bus fidelity.
- Any future retry, delivery-count, lock-renewal, or session work should extend the existing completed/dead-lettered/errored outcome model rather than hiding failures behind implicit requeues.
- If the processor ever becomes a long-running background loop instead of a synchronous snapshot drain, add explicit tests for ordering, cancellation, and duplicate-settlement behavior before changing the contract.

## Files Created/Modified

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — Added state-owned dequeue/requeue plus completed, dead-lettered, and errored outcome stores and inspection APIs that preserve the original envelope metadata on declared queue and canonical subscription entity paths.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — Implemented queue and subscription processor execution, auto-complete vs requeue behavior, actionable undeclared-topology diagnostics, handler-error propagation, and invalid-settlement guards over the shared state lifecycle APIs.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — Added focused processor tests covering queue completion, pending retention, auto-complete, undeclared processors, subscription dead-lettering on canonical paths, handler exceptions, missing ProcessErrorAsync, invalid settlement ordering, and blank-name guards.
- `.gsd/DECISIONS.md` — Recorded S03 lifecycle and requirement-validation decisions for processor batch consumption, failure reporting, and validated Service Bus observability requirements.
- `.gsd/KNOWLEDGE.md` — Captured the non-obvious processor snapshot-drain and auto-complete/requeue behavior that future Service Bus fidelity work should preserve or change deliberately.
- `.gsd/PROJECT.md` — Updated the project snapshot so downstream slices inherit that the Service Bus processor loop is now implemented and the remaining M001 work is Key Vault and Blob follow-on slices.
