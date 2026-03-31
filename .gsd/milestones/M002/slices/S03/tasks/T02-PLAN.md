---
estimated_steps: 4
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T02: Add a package-only queue redelivery test that follows the README seam

**Slice:** S03 — External consumer package proof
**Milestone:** M002

## Description

Turn the harness into meaningful proof by exercising the packaged Service Bus API from outside the producer project boundary. Add an xUnit test that uses `AddAzureServiceBusInMemory()`, resolves `IAzureServiceBusFactory`, declares queue topology, sends a message, fails the first processor run, asserts pending and errored state with `DeliveryCount == 2`, reruns processing explicitly, and asserts the completed outcome — all through package-consumer code only.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Public package seam in `README.md`, `AzureServiceBusRegistrationExtensions`, and `IAzureServiceBusFactory` | If the consumer test cannot follow the documented public API, stop and align the scenario with the package-facing seam instead of importing repo-only helpers. | Not applicable beyond the normal test run. | Reject any test that reaches into internal helpers or producer-only types outside the published package surface. |
| Queue redelivery behavior proved in `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` | If the consumer behavior diverges, fix the consumer scenario or surface a real producer regression; do not weaken the assertions into a smoke test. | Treat an unexpected hang as a processor/test bug and fail fast. | Reject assertions that skip the first failed attempt, the pending retry state, or the explicit second `StartProcessingAsync()` run. |
| Package restore output from T01 | If `dotnet test --no-restore` cannot compile because transitives or package references are wrong, fix the consumer project instead of adding a project reference. | Not applicable beyond the test run. | Reject a consumer test project that only builds because the restore path or package versions are incomplete. |

## Load Profile

- **Shared resources**: in-memory Service Bus state plus the consumer project's restored package graph
- **Per-operation cost**: one queue setup, one send, and two explicit processor runs inside a single xUnit test
- **10x breakpoint**: test isolation would fail first if multiple scenarios reused shared provider state, so each test should build a fresh `ServiceProvider`

## Negative Tests

- **Malformed inputs**: missing topology declaration, wrong queue name, or assertions against repo-only helpers instead of the public inspection surface
- **Error paths**: the first processor invocation must throw from the handler, record an errored outcome, and leave a pending message with incremented `DeliveryCount`
- **Boundary conditions**: the second explicit `StartProcessingAsync()` run must be the moment the message completes, proving there is no hidden background retry loop

## Steps

1. Add `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` and mirror the producer redelivery baseline using only package-consumer code paths.
2. Use `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, `AzureServiceBusProcessorOptions`, and `InMemoryServiceBusState` from the package to create the queue, send a message, and observe retry state.
3. Assert the first failed attempt leaves one pending message with `DeliveryCount == 2` and one errored outcome, then rerun the processor explicitly and assert the completed message also reports `DeliveryCount == 2`.
4. Update `samples/Azure.InMemory.ExternalConsumer/README.md` so the committed consumer harness explains that this test mirrors the packaged README seam and intentionally avoids producer-only helpers.

## Must-Haves

- [ ] `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` uses only public package APIs to build the scenario.
- [ ] The test proves delivery-count progression and pending/error visibility after the first failed processor run.
- [ ] The test proves the message completes only after the second explicit `StartProcessingAsync()` call.
- [ ] The sample README explains why this consumer scenario is meaningful package proof instead of a compile-only smoke test.

## Verification

- `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore --filter FullyQualifiedName~ExternalConsumerQueueRedeliveryTests`
- `test -s ./samples/Azure.InMemory.ExternalConsumer/README.md && rg -n "AddAzureServiceBusInMemory|IAzureServiceBusFactory|DeliveryCount|StartProcessingAsync" ./samples/Azure.InMemory.ExternalConsumer/README.md`

## Observability Impact

- Signals added/changed: the consumer proof now asserts pending, errored, and completed state transitions from outside the producer project boundary.
- How a future agent inspects this: run the focused consumer test and read `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` alongside the sample README.
- Failure state exposed: broken package transitives, seam drift, or retry regressions surface as direct xUnit assertion failures from the external consumer boundary.

## Inputs

- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` — standalone package-only consumer harness from T01.
- `README.md` — authoritative package-facing quickstart the consumer test should follow.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — public factory seam the consumer must use.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — public inspection APIs the consumer can assert against.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs` — producer-side redelivery baseline to mirror without project references.

## Expected Output

- `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` — package-only redelivery assertions proving queue retry visibility from an external consumer boundary.
- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` — any package/test dependency updates needed for the committed consumer test.
- `samples/Azure.InMemory.ExternalConsumer/README.md` — consumer-harness notes that explain how the test maps to the packaged README seam.
