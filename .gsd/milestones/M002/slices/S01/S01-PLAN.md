# S01: Observable Service Bus redelivery fidelity

**Goal:** Deepen Service Bus realism in the narrowest high-value area—redelivery, delivery-count progression, and max-delivery dead-letter behavior—while preserving the explicit registration/factory seam and deterministic processor model from M001.
**Demo:** After this: A focused Service Bus scenario proves that an unsuccessfully processed message is redelivered with incremented delivery count and automatically dead-lettered after the configured maximum, with all evidence visible through the existing in-memory test harness and no seam rewrite.

## Tasks
- [x] **T01: Blocked by a checkout mismatch because the M002 worktree does not contain the Azure.InMemory solution source tree.** — Retire the highest-risk behavioral unknown on the simplest entity path first. Extend the existing in-memory Service Bus files listed in Inputs rather than recreating the library; if those files are absent in the execution worktree, stop and report a checkout mismatch instead of inventing new project structure. Add a dedicated redelivery-focused queue test path, carry delivery-count and max-delivery metadata in the state-owned lifecycle, and change unsuccessful-processing behavior from terminal-only failure to explicit requeue/dead-letter progression without adding background timers or changing DI registration shape.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `InMemoryServiceBusState` lifecycle transitions | Fail fast with named diagnostics for undeclared queues, invalid requeue/dead-letter transitions, or missing max-delivery metadata instead of silently dropping or duplicating messages. | Not applicable; all state mutation remains in-process and synchronous, so any hang is a regression. | Preserve the stored body, `MessageId`, application properties, and current delivery count from the tracked envelope instead of rebuilding state from mutable handler input. |
| Queue processor execution in `InMemoryServiceBusFactory` | Requeue only the drained message that failed, increment delivery count deterministically, and dead-letter once the configured maximum is reached. | Not applicable; processor execution stays within the explicit `StartProcessingAsync()` run. | Reject malformed entity names or invalid settlement ordering with actionable failures instead of synthesizing topology or retrying unrelated messages. |

## Load Profile

- **Shared resources**: singleton `InMemoryServiceBusState`, queue pending buffers, and dead-letter outcome storage
- **Per-operation cost**: one drained envelope, one delivery-count update, and either one requeue or one dead-letter write per failed attempt
- **10x breakpoint**: poison-message loops or repeated failures would inflate pending/dead-letter history first, so tests must assert exact counts and delivery-count progression rather than broad presence only

## Negative Tests

- **Malformed inputs**: blank queue names and invalid max-delivery values should fail fast with named diagnostics
- **Error paths**: an unsuccessful handler should requeue the message for the next explicit processor run until the configured maximum is reached, then move it to dead-letter with an exhaustion reason
- **Boundary conditions**: the first failure should make the next run observe delivery count 2, and the final allowed failure should leave no extra pending copy behind

## Steps

1. Create `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` with queue-focused coverage for delivery-count progression, requeue-on-next-run behavior, and automatic dead-letter after the configured maximum.
2. Extend `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` with state-owned topology or envelope metadata for current delivery count and max-delivery tracking, plus inspection accessors that keep assertions inside the existing harness.
3. Update `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` so unsuccessful queue processing requeues deterministically for the next run, increments delivery count once per failed attempt, and dead-letters when the configured limit is exhausted while preserving existing completion semantics.
4. Adjust `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` only where current terminal-error assumptions now conflict with the redelivery contract.
5. Run the focused redelivery queue checks and the broader processor suite sequentially with `./Azure.InMemory.sln`.

## Must-Haves

- [ ] `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` proves queue redelivery and max-delivery dead-letter behavior through the existing factory seam
- [ ] Delivery count is persisted in the state-owned lifecycle and is observable in both pending and dead-letter assertions
- [ ] Unsuccessful queue processing re-delivers only on the next explicit processor run and never introduces background polling
- [ ] Max-delivery exhaustion moves the message to dead-letter and leaves no duplicate pending copy behind
- [ ] Focused queue redelivery and processor verification pass with `./Azure.InMemory.sln`
  - Estimate: 2h
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests
  - Blocker: The active M002 worktree lacks Azure.InMemory.sln and all expected src/ and tests/ C# files. The only visible copy of the solution is nested under .gsd/worktrees/M001/..., which should not be treated as the target tree for this task.
- [x] **T02: Restored the M002 root solution checkout from the sibling M001 snapshot and verified processor test discovery from ./Azure.InMemory.sln.** — Repair the execution precondition that blocked T01 before any product-code edits resume. Make the active M002 worktree root contain ./Azure.InMemory.sln, src/Azure.InMemory/..., and tests/Azure.InMemory.Tests/... in the expected locations; verify the work happens in this checkout rather than any nested .gsd/worktrees copy; and stop with named diagnostics if the root is still incomplete after the refresh. Once the files are present, confirm the solution can be discovered from the active root so the redelivery tasks can proceed without path ambiguity.
  - Estimate: 30m
  - Files: Azure.InMemory.sln, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - Verify: test -f ./Azure.InMemory.sln && test -f ./src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs && test -f ./src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs && test -f ./tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs && dotnet test ./Azure.InMemory.sln --list-tests --filter FullyQualifiedName~InMemoryServiceBusProcessorTests
- [x] **T03: Added queue redelivery and max-delivery dead-letter fidelity to the in-memory Service Bus processor while exposing delivery-count metadata through the existing test harness.** — Retire the highest-risk behavioral unknown once the correct checkout is available. Add queue-focused redelivery coverage, persist delivery-count and max-delivery metadata in InMemoryServiceBusState, and update InMemoryServiceBusFactory so an unsuccessfully processed queue message is requeued for the next explicit StartProcessingAsync() run, increments delivery count exactly once per failure, and moves to dead-letter when the configured maximum is exhausted. Update processor assertions only where the prior terminal-failure contract changes.
  - Estimate: 2h
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - Verify: dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests
- [x] **T04: Extended in-memory Service Bus redelivery fidelity to canonical subscription paths and closed the slice with full regression proof.** — Close the slice once queue redelivery is green by reusing the same bookkeeping for canonical subscription paths. Extend the redelivery suite so a topic publish is processed through <topic>/Subscriptions/<subscription>, prove delivery-count progression and max-delivery dead-letter stay isolated per canonical subscription path, update ingress/processor expectations only where the redelivery contract genuinely changes prior terminal-error assumptions, and capture any non-obvious invariant in .gsd/KNOWLEDGE.md for downstream packaging and consumer-proof work. Finish by running the focused redelivery suite, the broader Service Bus filter, and the full solution sequentially from the restored root.
  - Estimate: 2h
  - Files: tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs, .gsd/KNOWLEDGE.md
  - Verify: dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus && dotnet test ./Azure.InMemory.sln
