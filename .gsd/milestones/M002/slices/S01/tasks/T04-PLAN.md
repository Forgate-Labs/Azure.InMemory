---
estimated_steps: 1
estimated_files: 6
skills_used: []
---

# T04: Extend shared redelivery fidelity to canonical subscriptions and retire the Service Bus regression surface

Close the slice once queue redelivery is green by reusing the same bookkeeping for canonical subscription paths. Extend the redelivery suite so a topic publish is processed through <topic>/Subscriptions/<subscription>, prove delivery-count progression and max-delivery dead-letter stay isolated per canonical subscription path, update ingress/processor expectations only where the redelivery contract genuinely changes prior terminal-error assumptions, and capture any non-obvious invariant in .gsd/KNOWLEDGE.md for downstream packaging and consumer-proof work. Finish by running the focused redelivery suite, the broader Service Bus filter, and the full solution sequentially from the restored root.

## Inputs

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
- `.gsd/KNOWLEDGE.md`
- `.gsd/DECISIONS.md`

## Expected Output

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs proves queue and canonical subscription redelivery through the shared seam`
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs preserves canonical <topic>/Subscriptions/<subscription> routing assertions`
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs and InMemoryServiceBusFactory.cs share one queue/subscription redelivery bookkeeping model`
- `.gsd/KNOWLEDGE.md records the non-obvious redelivery invariant for downstream slices`

## Verification

dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests && dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus && dotnet test ./Azure.InMemory.sln
