---
estimated_steps: 5
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T01: Implement queue processor execution and state-backed completion/pending inspection

**Slice:** S03 — Processor execution and settlement observability
**Milestone:** M001

## Description

Close the `StartProcessingAsync()` stub for the simplest real processor path first, so S03 proves queue execution semantics and creates the durable inspection APIs that R006, R007, and R010 depend on.

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

## Verification

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue`

## Observability Impact

- Signals added/changed: inspectable queue pending-to-completed transitions and undeclared-queue startup failures.
- How a future agent inspects this: run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue` and inspect `InMemoryServiceBusState` completed/pending getters in the assertions.
- Failure state exposed: whether a queue message completed, stayed pending, or failed to start because the entity path was never declared.

## Inputs

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — stable processor/admin seam that must stay unchanged.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — current pending-envelope source of truth to extend with completion/pending lifecycle APIs.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — current in-memory sender/processor/admin implementation with the `StartProcessingAsync()` stub.
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — reference for handler/context wiring and error-callback shape.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — regression baseline and DI/state setup pattern.

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — queue processor coverage for completion, pending retention, auto-complete, and undeclared-start diagnostics.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — state-owned dequeue/requeue/completed inspection APIs for processor execution.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — queue processor implementation that drains pending messages and records completion outcomes.
