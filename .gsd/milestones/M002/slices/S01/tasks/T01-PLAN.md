---
estimated_steps: 26
estimated_files: 4
skills_used: []
---

# T01: Blocked by a checkout mismatch because the M002 worktree does not contain the Azure.InMemory solution source tree.

Retire the highest-risk behavioral unknown on the simplest entity path first. Extend the existing in-memory Service Bus files listed in Inputs rather than recreating the library; if those files are absent in the execution worktree, stop and report a checkout mismatch instead of inventing new project structure. Add a dedicated redelivery-focused queue test path, carry delivery-count and max-delivery metadata in the state-owned lifecycle, and change unsuccessful-processing behavior from terminal-only failure to explicit requeue/dead-letter progression without adding background timers or changing DI registration shape.

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

## Inputs

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`

## Verification

dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests

## Observability Impact

- Signals added/changed: queue delivery-count progression and max-delivery dead-letter transitions.
- How a future agent inspects this: run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue` and inspect `InMemoryServiceBusState` pending/dead-letter assertions.
- Failure state exposed: current delivery count, remaining pending copy vs dead-letter transition, and max-delivery exhaustion reason.
