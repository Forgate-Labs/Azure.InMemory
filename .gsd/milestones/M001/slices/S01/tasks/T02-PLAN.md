---
estimated_steps: 5
estimated_files: 6
skills_used:
  - error-handling-patterns
---

# T02: Implement Service Bus SDK and in-memory provider registration

**Slice:** S01 — Provider registration and focused factories
**Milestone:** M001

## Description

Implement the highest-risk seam first: Service Bus registration for SDK-backed and in-memory modes. The task must prove that the same `IAzureServiceBusFactory` abstraction resolves through DI for either backend, that the in-memory mode publishes a shared singleton state root for later slices, and that invalid same-resource registration is rejected with explicit errors.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| DI-registered `ServiceBusClient` / `ServiceBusAdministrationClient` | Throw an actionable `InvalidOperationException` that names the missing SDK dependency instead of silently creating clients. | Not applicable at execution time; the library must not make network calls or credential bootstrapping here. | Not applicable yet; the seam is DI-bound and should not parse external payloads in S01. |
| Resource registration mode selection | Throw an actionable `InvalidOperationException` when both SDK and in-memory registrations are applied for Service Bus. | Not applicable; registration is synchronous. | Not applicable; invalid state is represented as conflicting registrations, not payload parsing. |

## Load Profile

- **Shared resources**: singleton `InMemoryServiceBusState` and the DI container lifetime graph
- **Per-operation cost**: service registration plus service-provider construction; no Azure I/O in S01 tests
- **10x breakpoint**: accidental multiple state-root registrations or duplicated guards would show up as type/instance mismatches in the registration tests before runtime scale matters

## Negative Tests

- **Malformed inputs**: duplicate same-resource registration by calling both `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()` on one service collection
- **Error paths**: SDK mode without the required Azure SDK client registrations should fail with a clear message
- **Boundary conditions**: repeated resolution of the in-memory backend should return the same shared state-root instance

## Steps

1. Refine `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` so it is library-owned and large enough for sender, processor, and topology/admin creation without mirroring the entire SDK.
2. Implement `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` as a thin adapter over Azure SDK clients already registered by the host.
3. Implement `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` and `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, registering the state as a singleton root for later slices.
4. Add `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` with `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()` plus explicit conflict guards.
5. Add `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` covering SDK selection, in-memory selection, shared state reuse, and conflicting-registration failure behavior.

## Must-Haves

- [ ] `IAzureServiceBusFactory` remains focused and library-owned.
- [ ] `AddAzureServiceBusSdk()` binds to DI-registered Azure SDK clients instead of constructing them.
- [ ] `AddAzureServiceBusInMemory()` exposes a singleton `InMemoryServiceBusState`.
- [ ] Conflicting same-resource registrations fail fast with actionable text.
- [ ] Targeted Service Bus registration tests pass.

## Verification

- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests`
- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests.Conflicting`

## Observability Impact

- Signals added/changed: explicit conflict and missing-client exception messages plus assertions on resolved implementation types and singleton state identity
- How a future agent inspects this: run `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests`
- Failure state exposed: which Service Bus registration path failed, whether the wrong backend resolved, and whether the state root stopped being shared

## Inputs

- `src/Azure.InMemory/Azure.InMemory.csproj` — library package references and assembly identity from T01
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — test framework and project reference from T01
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — base Service Bus seam to implement
- `.gsd/DECISIONS.md` — D002 for explicit resource-scoped provider registration
- `.gsd/milestones/M001/slices/S01/S01-RESEARCH.md` — Service Bus contract sizing and DI guidance

## Expected Output

- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — Service Bus registration extensions and guards
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — SDK-backed Service Bus adapter
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — in-memory Service Bus factory
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — singleton in-memory state root
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — focused Service Bus registration tests
