# S02: In-memory Service Bus topology and message ingress

**Goal:** Make in-memory Service Bus ingress topology-aware so queue sends and topic publishes land on the correct pending entity paths and stay observable inside the `dotnet test` loop.
**Demo:** After this: A test creates a topic and subscription in memory, publishes a message, and can observe that the message is available to the in-memory Service Bus pipeline.

## Tasks
- [x] **T01: Added topology-gated in-memory queue ingress tests and rejected undeclared queue sends.** — Add dedicated Service Bus behavior coverage so S02 stops relying on DI smoke tests and gains a real queue-ingress contract. This task should create the new in-memory Service Bus test area, prove that queue topology must exist before send, and verify that the stored pending envelope preserves the body and metadata S03 will need later.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `IAzureServiceBusAdministration.CreateQueueAsync(...)` | Fail the test with a named assertion; do not let sender code create queue topology implicitly. | Not applicable; the in-memory admin path is synchronous and should return immediately. | Not applicable; queue creation is a direct method call, not parsed payload input. |
| `InMemoryServiceBusState` pending queue storage | Throw an actionable `InvalidOperationException` when send targets an unknown queue instead of silently inventing the entity path. | Not applicable; concurrent in-memory mutation should complete immediately, so any hang is a regression. | Preserve the caller-provided body, `MessageId`, and application properties in the stored envelope; invalid entity names should still fail fast through existing argument validation. |

## Load Profile

- **Shared resources**: singleton `InMemoryServiceBusState`, queue-topology dictionary, and per-queue pending-envelope buffers
- **Per-operation cost**: one topology lookup plus one envelope allocation and concurrent queue append per send
- **10x breakpoint**: memory growth from many pending envelopes would show up first, so the tests should assert exact queue counts and envelope ordering rather than relying on loose inspection

## Negative Tests

- **Malformed inputs**: empty or whitespace queue/entity names should continue failing via argument validation
- **Error paths**: sending to a queue path that was never created should fail with explicit text instead of silently creating a pending queue
- **Boundary conditions**: a freshly created queue starts empty, then contains exactly one pending envelope after one send

## Steps

1. Create `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` with focused in-memory queue-ingress coverage and DI/state resolution similar to the existing Service Bus registration tests.
2. Add a test that `CreateQueueAsync(...)` establishes topology and that sending to the created queue stores exactly one pending envelope with the expected body, `MessageId`, and application properties.
3. Add a negative test proving that sending to an unknown queue fails instead of auto-creating a pending buffer.
4. Update `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` so queue ingress requires declared topology, and only adjust `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` as needed to keep the sender thin over the state.

## Must-Haves

- [ ] Dedicated in-memory Service Bus ingress tests exist under `tests/Azure.InMemory.Tests/ServiceBus/InMemory/`.
- [ ] Queue creation remains explicit through `IAzureServiceBusAdministration`.
- [ ] Sending to a declared queue stores one pending envelope with preserved body and metadata.
- [ ] Sending to an undeclared queue fails with actionable diagnostics.
- [ ] Focused queue-ingress tests pass with the explicit `./Azure.InMemory.sln` path.
  - Estimate: 90m
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.Queue`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownQueue`
- [x] **T02: Added topology-aware topic publish fan-out so in-memory Service Bus enqueues cloned envelopes per subscription path and never on the topic name itself.** — Close the roadmap demo by making topic publish behavior topology-aware on the shared in-memory state. This task should prove that a publish to a created topic clones envelopes into each subscription entity path, never the topic name itself, and that the full Service Bus slice still verifies inside a single `dotnet test` run while the processor execution stub remains deferred to S03.

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

1. Extend `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` with topic/subscription publish coverage using the literal ``<topic>/Subscriptions/<subscription>`` entity-path format for assertions.
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
  - Estimate: 105m
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- `dotnet test ./Azure.InMemory.sln`
