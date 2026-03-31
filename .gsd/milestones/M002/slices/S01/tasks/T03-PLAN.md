---
estimated_steps: 1
estimated_files: 4
skills_used: []
---

# T03: Implement queue redelivery fidelity on the restored Service Bus seam

Retire the highest-risk behavioral unknown once the correct checkout is available. Add queue-focused redelivery coverage, persist delivery-count and max-delivery metadata in InMemoryServiceBusState, and update InMemoryServiceBusFactory so an unsuccessfully processed queue message is requeued for the next explicit StartProcessingAsync() run, increments delivery count exactly once per failure, and moves to dead-letter when the configured maximum is exhausted. Update processor assertions only where the prior terminal-failure contract changes.

## Inputs

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `.gsd/milestones/M002/slices/S01/tasks/T01-SUMMARY.md`
- `.gsd/DECISIONS.md`

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs proves queue delivery-count progression and max-delivery dead-letter behavior`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs carries observable delivery-count/max-delivery lifecycle metadata`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs requeues only the failed queue envelope on the next explicit processor run without background polling`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs reflects the redelivery contract where needed`

## Verification

dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.Queue && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests
