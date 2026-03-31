---
estimated_steps: 5
estimated_files: 7
skills_used: []
---

# T02: Author one package-facing quickstart that teaches the real Service Bus seam

**Slice:** S02 — Internal-ready package surface and docs
**Milestone:** M002

## Description

Turn the placeholder root `README.md` into the single package-facing guide an internal consumer can follow without guessing hidden setup. Keep it truthful to the focused factory seam, the explicit topology requirements, and S01's deterministic rerun semantics so S03 can later use the same guide for a fresh consumer proof.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Public Service Bus registration and factory seam in `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` and `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` | If the docs cannot point to a real public API, stop and reconcile the README with the existing seam instead of inventing package-only abstractions. | Not applicable; documentation work is local file editing. | Do not publish incorrect imports or call patterns such as raw Azure SDK clients when the seam requires `IAzureServiceBusFactory`. |
| Redelivery/inspection behavior proved in `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, and the Service Bus tests | If docs drift from the tested behavior, correct the README rather than softening the wording. | Not applicable; behavior truth comes from the existing code and tests. | Never imply background retries, topic-local pending queues, or subscription paths that differ from canonical `<topic>/Subscriptions/<subscription>`. |

## Load Profile

- **Shared resources**: the single-sourced `README.md` package readme and the existing public Service Bus seam it documents
- **Per-operation cost**: one authoritative doc update that replaces the placeholder root guide
- **10x breakpoint**: documentation drift across multiple guides would break first, so this task keeps one authoritative package-facing quickstart instead of splitting examples across new docs files

## Negative Tests

- **Malformed inputs**: wrong namespaces, missing `PackageReference`/install instructions, or examples that skip topology declaration must be treated as documentation bugs
- **Error paths**: README examples must explain that a successful handler without `CompleteMessageAsync(...)` leaves the message pending unless `AutoCompleteMessages: true` is chosen
- **Boundary conditions**: docs must cover both queue setup and canonical subscription inspection while keeping `InMemoryServiceBusState` framed as test-only observability

## Steps

1. Replace the placeholder root `README.md` with an internal-consumer overview, installation snippet, and a short explanation of the explicit Azure resource factory model.
2. Add a concrete Service Bus quickstart that shows `using Azure.InMemory.DependencyInjection;`, resolving `IAzureServiceBusFactory`, declaring topology, and calling `AddAzureServiceBusInMemory()` from DI.
3. Document processor settlement and retry truthfully: explain explicit `CompleteMessageAsync(...)`, the optional `AutoCompleteMessages: true` path, and that failed deliveries reappear only on the next explicit `StartProcessingAsync()` run.
4. Document the test-only `InMemoryServiceBusState` inspection surface, including canonical subscription entity paths in the form `<topic>/Subscriptions/<subscription>`.
5. Keep the README package-safe by avoiding repo-only assumptions and by framing external-consumer/local-feed proof as the next-slice concern rather than pretending S02 already delivered it.

## Must-Haves

- [ ] `README.md` explains installation and DI registration for the packaged library.
- [ ] The quickstart uses the real public namespaces and `IAzureServiceBusFactory` seam rather than raw Azure SDK clients.
- [ ] The docs explicitly preserve S01's deterministic redelivery semantics and canonical subscription-path rule.
- [ ] `InMemoryServiceBusState` is documented as a test-only inspection surface, not the primary application seam.

## Verification

- `test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md`

## Inputs

- `README.md` — placeholder package-facing doc to replace.
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — authoritative DI registration method names and namespaces.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — public sender/processor/administration seam the docs must teach.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — explicit processor/settlement semantics the quickstart must describe truthfully.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — test-only inspection APIs and canonical subscription entity-path behavior.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — current settlement baseline to keep README examples truthful.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` — S01 retry behavior the docs must preserve.

## Expected Output

- `README.md` — package-safe internal-consumer quickstart aligned with the current public Service Bus seam.
