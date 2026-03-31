---
estimated_steps: 5
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T02: Fan out topic publishes to subscription paths and close slice proof

**Slice:** S02 — In-memory Service Bus topology and message ingress
**Milestone:** M001

## Description

Close the roadmap demo by making topic publish behavior topology-aware on the shared in-memory state. This task should prove that a publish to a created topic clones envelopes into each subscription entity path, never the topic name itself, and that the full Service Bus slice still verifies inside a single `dotnet test` run while the processor execution stub remains deferred to S03.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Topic/subscription topology lookup in `InMemoryServiceBusState` | Throw an actionable `InvalidOperationException` for unknown topics instead of silently dropping or auto-creating paths. | Not applicable; topology lookup is in-process and should complete immediately. | Invalid topic/subscription/entity names should still fail fast via argument validation before any state mutation. |
| Sender batch/publish routing in `InMemoryServiceBusFactory` | Fail fast on cancellation or null messages and avoid partially inventing topology. | Not applicable; the in-memory send loop should be immediate. | Each subscription should receive its own copied envelope with preserved body and metadata rather than sharing mutable state. |

## Load Profile

- **Shared resources**: topic-to-subscription map and one pending-envelope queue per subscription entity path
- **Per-operation cost**: one topic lookup plus one envelope clone and concurrent queue append per subscription per message
- **10x breakpoint**: fan-out memory/copy cost grows with subscription count and batch size, so focused tests should assert correct counts per subscription and zero topic-local pending messages

## Negative Tests

- **Malformed inputs**: empty or whitespace topic/subscription names should continue failing via argument validation
- **Error paths**: publishing to an undeclared topic should fail with explicit diagnostics
- **Boundary conditions**: one topic with multiple subscriptions receives independent envelope copies, and the topic entity path itself remains empty

## Steps

1. Extend `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` with topic/subscription publish coverage using the literal `<topic>/Subscriptions/<subscription>` entity-path format for assertions.
2. Update `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` to distinguish queue sends from topic publishes and clone one pending envelope per existing subscription.
3. Keep `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` thin by routing both single-message and batch sends through the same topology-aware state behavior, while leaving `StartProcessingAsync()` as the explicit S03 stub.
4. Add negative coverage proving the topic name itself does not accumulate pending messages and that publishing to an undeclared topic fails loudly.
5. Run the focused ingress tests and the full solution with `./Azure.InMemory.sln` to close R003/R005 proof for the slice.

## Must-Haves

- [ ] Topic publish behavior requires declared topic topology.
- [ ] Each created subscription path receives its own pending envelope copy when the topic is published to.
- [ ] The topic name itself does not become a pending-message queue.
- [ ] `InMemoryServiceBusProcessor.StartProcessingAsync()` remains the explicit S03 `NotSupportedException` boundary.
- [ ] Focused ingress tests and the full solution both pass.

## Verification

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- `dotnet test ./Azure.InMemory.sln`

## Observability Impact

- Signals added/changed: subscription-path pending-envelope counts, explicit unknown-topic ingress errors, and assertions that the topic entity path stays empty.
- How a future agent inspects this: run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests` and inspect `GetPendingMessages($"{topic}/Subscriptions/{subscription}")` in the test setup.
- Failure state exposed: whether fan-out happened to every subscription, whether envelopes preserved message metadata, and whether the sender accidentally routed to the wrong entity path.

## Inputs

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — queue-ingress coverage from T01 to extend into topic/subscription scenarios.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — topology-aware ingress logic to extend from queue routing into topic fan-out.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — sender plumbing that must stay thin while routing through the shared state.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — processor/admin contracts whose public shape must remain stable for S03.

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — topic/subscription fan-out proof and negative routing coverage.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — topic-aware ingress routing that clones envelopes to subscription paths.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — sender batch/single-message flow still delegating to topology-aware state logic.
