---
estimated_steps: 1
estimated_files: 5
skills_used: []
---

# T02: Restore a usable M002 solution checkout and lock the execution boundary

Repair the execution precondition that blocked T01 before any product-code edits resume. Make the active M002 worktree root contain ./Azure.InMemory.sln, src/Azure.InMemory/..., and tests/Azure.InMemory.Tests/... in the expected locations; verify the work happens in this checkout rather than any nested .gsd/worktrees copy; and stop with named diagnostics if the root is still incomplete after the refresh. Once the files are present, confirm the solution can be discovered from the active root so the redelivery tasks can proceed without path ambiguity.

## Inputs

- `.gsd/milestones/M002/slices/S01/tasks/T01-SUMMARY.md`
- `.gsd/DECISIONS.md`

## Expected Output

- `Usable M002 root solution checkout is present in the active worktree`
- `Processor test discovery succeeds from ./Azure.InMemory.sln without targeting nested worktrees`

## Verification

test -f ./Azure.InMemory.sln && test -f ./src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs && test -f ./src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs && test -f ./tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs && dotnet test ./Azure.InMemory.sln --list-tests --filter FullyQualifiedName~InMemoryServiceBusProcessorTests

## Observability Impact

- Signals added/changed: canonical subscription delivery-count progression and max-delivery dead-letter evidence.
- How a future agent inspects this: run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests` and assert against canonical `<topic>/Subscriptions/<subscription>` paths in `InMemoryServiceBusState`.
- Failure state exposed: per-subscription delivery count, dead-letter exhaustion, and whether topic-path emptiness was preserved.
