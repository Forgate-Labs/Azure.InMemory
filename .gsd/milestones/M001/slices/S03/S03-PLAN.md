# S03: Processor execution and settlement observability

**Goal:** Implement in-memory Service Bus processor execution on top of the S02 pending-envelope state so tests can assert completion, dead-letter, pending, and errored outcomes without leaving `dotnet test`.
**Demo:** After this: An in-memory processor consumes a published message and the test can assert whether it was completed, dead-lettered, left pending, or errored.

## Tasks
- [x] **T01: Added in-memory queue processor execution with completed-state inspection and queue settlement tests.** — - Why: Close the `StartProcessingAsync()` stub for the simplest real processor path first, so S03 proves queue execution semantics and creates the durable inspection APIs that R006/R007/R010 depend on.
- Files: `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- Do: Add state-owned dequeue/requeue/completion outcome helpers plus queue processor execution that builds `AzureServiceBusReceivedMessageContext` from stored envelopes, validates declared entity paths before processing, supports explicit completion, and requeues messages that return successfully without settlement when `AutoCompleteMessages` is false.
- Verify: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue`
- Done when: Focused queue processor tests prove declared queues can process pending envelopes, explicit completion moves messages into an inspectable completed bucket, successful-but-unsettled handlers leave messages pending, `AutoCompleteMessages` completes on success, and undeclared queue processors fail with actionable startup diagnostics.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `InMemoryServiceBusState` lifecycle transitions | Fail fast with actionable exceptions for undeclared queues or invalid settlement sequences instead of silently dropping messages. | Not applicable; in-memory state mutation should complete immediately, so any hang is a regression. | Preserve the stored body, `MessageId`, and application properties from the pending envelope rather than rebuilding from mutable handler input. |
| `AzureServiceBusReceivedMessageContext` completion callbacks | Keep the message pending unless the handler explicitly completes it or `AutoCompleteMessages` is enabled after a successful return. | Not applicable; callback invocation is synchronous within the processor loop. | Reject double-complete or complete-after-dead-letter attempts so tests can see deterministic failure state. |

## Load Profile

- **Shared resources**: singleton `InMemoryServiceBusState`, per-entity pending buffers, and the new completed-outcome storage
- **Per-operation cost**: one dequeue, one context allocation, optional one requeue or completion record write per processed message
- **10x breakpoint**: pending/completed state growth is the first pressure point, so tests should assert exact counts and ordering rather than relying on incidental iteration behavior

## Negative Tests

- **Malformed inputs**: blank queue/entity names should still fail via existing argument validation before processor startup
- **Error paths**: starting a queue processor for an undeclared entity should fail with a named diagnostic instead of silently creating state
- **Boundary conditions**: a handler that returns without settling leaves the envelope pending, while `AutoCompleteMessages = true` completes the same path automatically

## Steps

1. Create `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` with queue-focused processor coverage and DI/state resolution mirroring the ingress tests.
2. Extend `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` with state-owned pending dequeue/requeue and completed-outcome inspection APIs that preserve envelope metadata.
3. Implement queue processor startup in `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` so it validates the queue, drains the current pending batch, constructs `AzureServiceBusReceivedMessageContext`, and routes explicit completion back into state.
4. Honor `AzureServiceBusProcessorOptions.AutoCompleteMessages` after successful handler return while keeping successful-but-unsettled messages pending when auto-complete is disabled.
5. Add negative coverage for undeclared queue startup and invalid settlement ordering if the callback tries to settle the same message twice.

## Must-Haves

- [ ] `InMemoryServiceBusState` exposes inspectable completed outcomes without losing the original envelope metadata.
- [ ] Queue `StartProcessingAsync()` consumes the current pending batch for a declared queue and preserves sequential, deterministic behavior.
- [ ] Explicit `CompleteMessageAsync()` moves the envelope out of pending state into a completed bucket.
- [ ] Successful handlers that do not settle stay pending unless `AutoCompleteMessages` is enabled.
- [ ] Focused queue processor tests pass with `./Azure.InMemory.sln`.
  - Estimate: 105m
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue`
- [x] **T02: Added canonical subscription processing plus inspectable dead-letter and error outcomes for in-memory Service Bus processors.** — - Why: Close the remaining slice demo on the canonical subscription path, add truthful dead-letter/error observability, and prove S03 preserves the infrastructure-free Service Bus loop end to end.
- Files: `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- Do: Extend the state and processor to record dead-lettered and errored outcomes, route handler exceptions through `ProcessErrorAsync` without swallowing them, preserve dead-letter reasons on canonical subscription entity paths, and keep ingress behavior green while queue and subscription processors share the same lifecycle logic.
- Verify: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests` then `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus` and `dotnet test ./Azure.InMemory.sln`
- Done when: Focused processor tests prove subscription processors can dead-letter published messages on `<topic>/Subscriptions/<subscription>`, handler failures create inspectable errored outcomes even when `ProcessErrorAsync` is null, invalid settlement sequences are surfaced deterministically, and the Service Bus/full-solution regressions stay green.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `ProcessMessageAsync` handler execution | Persist an errored outcome with entity path, `MessageId`, and exception, then invoke `ProcessErrorAsync` if configured without swallowing the original failure context. | Not applicable; in-memory handler invocation should complete immediately, so a hang indicates a regression in the processor loop or test handler. | Treat conflicting settlement calls or invalid dead-letter reasons as processor errors recorded in state rather than silently ignoring them. |
| Canonical subscription-path routing from S02 | Fail fast if the subscription entity path was never declared; do not create a topic-local pending queue during processing. | Not applicable; lookup is in-process and deterministic. | Dead-letter the exact stored envelope for the subscription path, preserving body and cloned application properties for later assertions. |

## Load Profile

- **Shared resources**: singleton `InMemoryServiceBusState`, subscription pending buffers, and new dead-letter/error outcome stores
- **Per-operation cost**: one dequeue plus one lifecycle write and optional `ProcessErrorAsync` callback per processed message
- **10x breakpoint**: error/dead-letter history accumulation will grow state fastest, so tests should assert bounded counts and exact entity paths rather than scanning loosely

## Negative Tests

- **Malformed inputs**: blank topic/subscription names and invalid settlement ordering should fail with named diagnostics
- **Error paths**: handler exceptions must produce inspectable errored outcomes whether or not `ProcessErrorAsync` is configured
- **Boundary conditions**: publishing one message to a topic with one subscription yields exactly one dead-letter or errored record on the canonical subscription entity path and leaves the topic path empty

## Steps

1. Extend `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` with subscription dead-letter, handler-exception, and invalid-settlement coverage built on top of S02 ingress behavior.
2. Add dead-lettered and errored outcome records plus public inspection getters to `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, preserving dead-letter reason, exception, and original envelope metadata.
3. Update `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` so queue and subscription processors share lifecycle handling, route handler exceptions through `ProcessErrorAsync`, and record error state even when no callback is configured.
4. Prove canonical subscription processing by creating a topic/subscription, publishing through the topic sender, and asserting that the processor observes and settles the `<topic>/Subscriptions/<subscription>` envelope instead of a topic-local queue.
5. Run the focused processor suite, the broader Service Bus suite, and the full solution sequentially with `./Azure.InMemory.sln`.

## Must-Haves

- [ ] `InMemoryServiceBusState` exposes inspectable dead-lettered and errored outcomes keyed by queue or canonical subscription entity path.
- [ ] Subscription processors consume from `<topic>/Subscriptions/<subscription>` and never invent a topic-local processing buffer.
- [ ] `DeadLetterMessageAsync()` preserves the reason text on the stored dead-letter outcome.
- [ ] Handler exceptions and invalid settlement sequences surface through state and `ProcessErrorAsync` without being swallowed.
- [ ] Focused processor, Service Bus, and full-solution verification all pass sequentially.
  - Estimate: 120m
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- `dotnet test ./Azure.InMemory.sln`
