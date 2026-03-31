---
estimated_steps: 4
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T01: Add queue-ingress behavior tests and enforce declared queue topology

**Slice:** S02 — In-memory Service Bus topology and message ingress
**Milestone:** M001

## Description

Add dedicated Service Bus behavior coverage so S02 stops relying on DI smoke tests and gains a real queue-ingress contract. This task should create the new in-memory Service Bus test area, prove that queue topology must exist before send, and verify that the stored pending envelope preserves the body and metadata S03 will need later.

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

## Verification

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.Queue`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownQueue`

## Observability Impact

- Signals added/changed: queue-specific pending-envelope visibility and explicit unknown-queue ingress errors.
- How a future agent inspects this: run the focused `InMemoryServiceBusIngressTests` queue cases and inspect `InMemoryServiceBusState.GetPendingMessages(queueName)` in the assertions.
- Failure state exposed: whether the queue topology was created, whether the sender stored the envelope on the right entity path, and whether invalid queue ingress now fails loudly.

## Inputs

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — existing sender/admin seam that the queue-ingress behavior must preserve.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — current thin in-memory sender/administration implementation.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — current queue/topic/subscription state and pending-envelope storage.
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — existing Service Bus test style and DI setup pattern.

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — focused queue-ingress proof and negative coverage.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — topology-aware queue-ingress behavior.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — sender plumbing that still delegates queue ingress to state.
