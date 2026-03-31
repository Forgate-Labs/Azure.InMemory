# M001/S03 — Research

**Date:** 2026-03-30

## Summary

S03 owns **R006**, **R007**, and **R010**, and it supports **R003** and **R005**. The codebase already has the right public seam for this slice: `IAzureServiceBusFactory` exposes queue/subscription processor creation, `AzureServiceBusReceivedMessageContext` already models completion and dead-letter callbacks, and the SDK adapter shows the intended handler shape. What is still missing is only the in-memory execution path. S02 intentionally centralized topology and pending envelopes inside `InMemoryServiceBusState` and preserved canonical subscription entity paths, so S03 can add processor behavior without widening the factory API.

The remaining risk is execution semantics, not ingress. `InMemoryServiceBusProcessor` is still a `NotSupportedException` stub. To keep `dotnet test` infrastructure-free and preserve S02 behavior, S03 should consume the existing pending buffers instead of inventing a new transport surface. The loaded `error-handling-patterns` skill is directly relevant here: apply **Fail Fast**, **Preserve Context**, **Don’t Swallow Errors**, and **Handle at Right Level** by rejecting invalid settlement sequences, recording entity path/message id/exception in durable harness state, and ensuring handler failures remain observable even if a test does not wire `ProcessErrorAsync`.

## Recommendation

Implement processor behavior around **state-owned message lifecycle transitions**, not processor-owned ad hoc lists. Extend `InMemoryServiceBusState` with dequeue/settle/error APIs plus public inspection getters for completed, dead-lettered, and errored outcomes. Then keep `InMemoryServiceBusProcessor` thin: resolve the correct entity path, pull pending envelopes from state, build `AzureServiceBusReceivedMessageContext` from the stored envelope, invoke the configured handler, and push the envelope into the correct lifecycle bucket.

Treat `StartProcessingAsync()` as the explicit test-driven execution trigger for this milestone: validate the entity exists, drain the currently pending queue for that entity path, and move each message to completed, dead-lettered, back to pending, or errored depending on settlement and exceptions. Keep the public factory seam unchanged. Honor `AzureServiceBusProcessorOptions.AutoCompleteMessages` because that option already exists and the Azure Service Bus processor docs use it as the standard fallback when a handler returns successfully without manual settlement. `MaxConcurrentCalls` does not need true concurrency for M001 unless tests prove a need; truthful settlement and observability matter more than simulated throughput.

## Implementation Landscape

### Key Files

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — Stable public seam. `AzureServiceBusReceivedMessageContext` already exposes the exact completion/dead-letter callbacks the in-memory processor needs, so S03 should not need interface changes.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — Current source of truth for topology and pending envelopes. This is the right place to add lifecycle stores/getters such as completed, dead-lettered, and errored execution records, plus dequeue/requeue/settlement helpers so processor logic does not manipulate `ConcurrentQueue` internals directly.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — `InMemoryServiceBusProcessor` is still the stubbed boundary. This file needs the main execution change: inject state into the processor, create contexts from stored envelopes, invoke `ProcessMessageAsync` / `ProcessErrorAsync`, and implement `StartProcessingAsync()` / `StopProcessingAsync()` semantics.
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — Behavioral reference. The in-memory processor should mirror how the SDK adapter builds `AzureServiceBusReceivedMessageContext`, defaults dead-letter reasons, and routes failures through `ProcessErrorAsync`.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — Existing S02 guardrail. Keep these tests green to prove S03 did not regress topology-aware ingress or canonical subscription-path routing.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/` — Best place for a new focused file such as `InMemoryServiceBusProcessorTests.cs` covering queue completion, subscription dead-letter, unsettled messages staying pending, handler exception observability, and optional autocomplete behavior.

### Build Order

1. **Extend `InMemoryServiceBusState` first** with lifecycle buckets and atomic message-transition helpers. This retires the main design risk early and preserves S02’s pending-envelope contract.
2. **Implement `InMemoryServiceBusProcessor` against those state APIs.** Keep the factory seam unchanged and mirror the SDK adapter’s context construction.
3. **Add focused processor tests before broad verification.** Prove one queue-complete path, one subscription-dead-letter path, one unsettled path, and one error path with explicit state assertions.
4. **Run Service Bus-wide and full-solution verification sequentially** using `./Azure.InMemory.sln` to preserve the infrastructure-free loop and avoid the known shared-output race from parallel `dotnet test` runs.

## Constraints / Notes

- S03 should keep treating queue processors as direct queue entity paths and subscription processors as canonical `<topic>/Subscriptions/<subscription>` paths. Do not reintroduce a topic-local pending queue.
- S02 already preserved body bytes, `MessageId`, and application properties in `InMemoryServiceBusEnvelope`; S03 should reuse that envelope as the source for processor callbacks instead of reconstructing from the original sender message.
- The Azure Service Bus processor docs confirm the expected M001 baseline: `AutoCompleteMessages = false` with explicit `CompleteMessageAsync(...)`, optional `DeadLetterMessageAsync(...)`, and a separate `ProcessErrorAsync` callback. Matching that shape is enough for this slice.
- The `error-handling-patterns` skill gives four rules that should directly shape the implementation:
  - **Fail Fast** — reject blank entity paths and invalid double-settlement attempts immediately.
  - **Preserve Context** — error records should include at least entity path and exception, and preferably `MessageId` when available.
  - **Don’t Swallow Errors** — persist errored outcomes in state even when `ProcessErrorAsync` is null.
  - **Handle at Right Level** — state owns durable observability; processor owns handler invocation; tests assert through state rather than relying only on thrown exceptions.

## Skill Discovery Suggestions

- Already installed and directly relevant: `error-handling-patterns`.
- Promising uninstalled skill for the core messaging tech:
  - `npx skills add sickn33/antigravity-awesome-skills@azure-servicebus-dotnet`
- Promising uninstalled skills for richer .NET test guidance:
  - `npx skills add novotnyllc/dotnet-artisan@dotnet-testing`
  - `npx skills add kevintsengtw/dotnet-testing-agent-skills@dotnet-testing-unit-test-fundamentals`

## Verification

- Focused processor coverage:
  - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests`
- Service Bus regression sweep:
  - `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`
- Authoritative solution pass:
  - `dotnet test ./Azure.InMemory.sln`
