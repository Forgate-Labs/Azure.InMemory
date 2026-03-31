---
estimated_steps: 5
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T02: Add subscription dead-letter and error observability, then close Service Bus regressions

**Slice:** S03 — Processor execution and settlement observability
**Milestone:** M001

## Description

Close the remaining slice demo on the canonical subscription path, add truthful dead-letter/error observability, and prove S03 preserves the infrastructure-free Service Bus loop end to end.

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

## Verification

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- `dotnet test ./Azure.InMemory.sln`

## Observability Impact

- Signals added/changed: inspectable dead-letter and errored outcomes, preserved dead-letter reasons, and `ProcessErrorAsync` callback context.
- How a future agent inspects this: run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests` and assert against `InMemoryServiceBusState` dead-letter/error getters keyed by canonical entity path.
- Failure state exposed: which message failed, where it failed, why it was dead-lettered, and what exception the handler or settlement callback raised.

## Inputs

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — queue processor coverage from T01 to extend into dead-letter and error paths.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — canonical subscription-path ingress behavior that must stay green while processors are added.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — queue lifecycle state from T01 to extend with dead-letter/error observability.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — queue processor implementation from T01 to extend across subscription and error handling.
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — reference for dead-letter defaulting and `ProcessErrorAsync` behavior.

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — subscription dead-letter, error-path, and invalid-settlement proof plus full focused processor coverage.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — inspectable dead-letter and errored outcome stores for processor observability.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — shared queue/subscription processor lifecycle logic with dead-letter and error routing.
