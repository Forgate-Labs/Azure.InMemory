# S02 — Research

**Date:** 2026-03-30

## Summary

S02 primarily owns **R005** (in-memory Service Bus send/receive availability) and directly supports **R003** (keep the loop inside `dotnet test`) plus the future **R006** settlement work. The codebase already has the right seam for this slice: `IAzureServiceBusFactory` exposes sender, administration, and processor abstractions; `InMemoryServiceBusAdministration` can already create queues/topics/subscriptions; and `InMemoryServiceBusState` already exposes queue/topic/subscription existence plus pending-envelope inspection. The missing behavior is routing, not composition.

The current in-memory sender path is too permissive for S02. `InMemoryServiceBusSender.SendAsync()` and `SendBatchAsync()` call `InMemoryServiceBusState.Enqueue(entityPath, message)`, and `Enqueue` uses `_pendingMessages.GetOrAdd(...)`. That means sending to an unknown entity silently creates a pending queue, and sending to a **topic** only stores a message under the topic name instead of distributing copies to `topic/Subscriptions/<subscription>` queues. So topology creation exists, but ingress does not yet honor it.

The safest S02 implementation is therefore to keep the public factory seam stable and move message routing/topology validation into `InMemoryServiceBusState`, following the same pattern already used by Blob and Key Vault where the factory is thin and the state owns behavior. This slice should stop at truthful ingress and observable pending messages; `InMemoryServiceBusProcessor.StartProcessingAsync()` should remain the explicit `NotSupportedException` stub until S03.

## Recommendation

Implement S02 as a **state-driven ingress slice**:

1. keep `IAzureServiceBusFactory` unchanged unless a very small inspection helper is needed for test readability;
2. teach `InMemoryServiceBusState` to distinguish queue sends from topic publishes;
3. route topic publishes into each existing subscription entity path instead of creating a topic-local pending queue;
4. fail fast or otherwise explicitly handle unknown entities so `CreateQueueAsync` / `CreateTopicAsync` / `CreateSubscriptionAsync` are meaningful;
5. prove the behavior with dedicated in-memory Service Bus tests rather than only DI registration tests.

This keeps the slice narrow, advances R005 truthfully, and preserves S03’s processor/settlement boundary. The current envelope shape (`Body`, `MessageId`, application properties, `EnqueuedAt`) is already sufficient for S02 fan-out and gives S03 enough data to build received-message contexts without widening the public seam now.

## Implementation Landscape

### Key Files

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — **primary S02 implementation file**. Today it tracks queues, topics, subscriptions, and pending envelopes, but `Enqueue` bypasses topology rules by auto-creating per-entity queues. This is the natural place to add routing/validation logic and any narrow inspection helpers needed by tests.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — thin adapter over state. `InMemoryServiceBusAdministration` already creates topology; `InMemoryServiceBusSender` should remain thin and delegate to smarter state logic; `InMemoryServiceBusProcessor` should stay stubbed for S03.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — stable public seam. It already exposes the right sender/admin/processor contracts. Only touch this if S02 needs a minimal test-oriented state helper rather than hard-coding subscription entity-path strings in tests.
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — existing smoke coverage for registration only. Keep it green, but do not try to stretch it into ingress coverage.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — **recommended new test file** for S02. There is currently no dedicated Service Bus behavior test folder, so adding one is the cleanest way to prove queue and topic/subscription ingress without mixing concerns into DI tests.

### Build Order

1. **Write the failing S02 behavior tests first.** Add focused tests for:
   - queue creation + send => pending queue envelope exists;
   - topic + subscription creation + publish => pending envelope appears under the subscription entity path, not just the topic name;
   - published envelope preserves body, `MessageId`, and application properties;
   - optional: unknown entity send does not silently create valid topology.
2. **Implement routing in `InMemoryServiceBusState`.** Replace the current `Enqueue` semantics with a topology-aware path such as:
   - existing queue => append one envelope to that queue;
   - existing topic => append one cloned envelope per known subscription path;
   - unknown entity => throw actionable `InvalidOperationException` or otherwise make topology misuse explicit.
3. **Wire sender to the new state behavior.** `InMemoryServiceBusSender` should simply call the new routing method for single and batch sends.
4. **Leave processor execution alone.** Keep `StartProcessingAsync()` as the current `NotSupportedException`; S02 should not start pulling messages off queues yet.
5. **Run the focused tests, then the full solution.** This preserves the R003 invariant that the slice still verifies under infrastructure-free `dotnet test`.

### Verification Approach

Use the explicit solution path from the knowledge log.

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- `dotnet test ./Azure.InMemory.sln`

Observable proof for S02 should be:

- `CreateQueueAsync`, `CreateTopicAsync`, and `CreateSubscriptionAsync` establish topology in `InMemoryServiceBusState`;
- sending to a queue creates a pending envelope under that queue name;
- sending to a topic creates pending envelopes under each subscription entity path;
- the stored envelope retains body bytes, `MessageId`, and application properties;
- the full suite remains green with no Azure or Docker dependency.

## Constraints

- `Directory.Build.props` enforces `net10.0`, nullable enabled, analyzers on, and warnings-as-errors. Any new helper surface must compile cleanly with no nullable leaks.
- `InMemoryServiceBusState.GetSubscriptionEntityPath(...)` is currently `internal`, so tests in `tests/Azure.InMemory.Tests` cannot call it directly. Either assert against the literal `"<topic>/Subscriptions/<subscription>"` format or add a narrow public/test-oriented helper.
- The knowledge log says to use a **single authoritative** `dotnet test ./Azure.InMemory.sln` run when collecting proof; parallel test runs in this worktree can cause transient build-copy noise.

## Common Pitfalls

- **Leaving `Enqueue` topology-blind** — if send still auto-creates `_pendingMessages` entries for arbitrary entity names, topology creation becomes meaningless and R005 is only half-delivered.
- **Routing topic messages to the topic name itself** — subscription processors are created against `topic/Subscriptions/<subscription>`, so S02 must populate those entity paths.
- **Accidentally pulling S03 forward** — do not consume, complete, dead-letter, or error messages yet. S02 only needs ingress plus observability that the message is available for later processing.

## Open Risks

- Exact behavior for publishing to an existing topic with **zero subscriptions** is still a design choice. The roadmap only requires the created topic/subscription scenario, so the slice should avoid broadening into full Service Bus fidelity unless a test truly needs it.
- If S02 adds inspection helpers for subscription pending messages, keep them narrow; broader harness/settlement surfaces still belong to S03/R007-R010.

## Skills Discovered

No installed skill from `<available_skills>` directly targets .NET Azure Service Bus implementation. One relevant external skill was discoverable but not installed.

| Technology | Skill | Status |
|------------|-------|--------|
| Azure Service Bus (.NET) | `sickn33/antigravity-awesome-skills@azure-servicebus-dotnet` | available |
| .NET test ergonomics | none directly relevant found from quick search | none found |
