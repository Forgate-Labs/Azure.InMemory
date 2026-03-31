---
estimated_steps: 4
estimated_files: 7
skills_used:
  - error-handling-patterns
---

# T05: Prove mixed-resource composition and final registration behavior

**Slice:** S01 — Provider registration and focused factories
**Milestone:** M001

## Description

Close the slice with integration-style DI tests that compose different provider modes across the three resources in one service collection. This task turns the registration suite into the authoritative proof for S01 by showing that Service Bus, Blob, and Key Vault can each choose SDK or in-memory mode independently while the full `dotnet test` loop stays infrastructure-free.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Prior resource registration extensions | Tighten shared guards or helper code until mixed composition resolves the correct backend for every resource. | Not applicable; service registration and provider construction are synchronous. | Not applicable; the slice verifies DI composition, not external payload parsing. |
| Test composition across resources | Keep failures isolated by resource-specific assertions so one broken seam does not mask the others. | Not applicable; the suite runs fully in-process. | Not applicable; invalid state is expressed as wrong DI registrations or missing shared state roots. |

## Load Profile

- **Shared resources**: service-provider construction, singleton in-memory state roots, and any shared registration guard helpers
- **Per-operation cost**: build a service collection, register three resources, resolve factories/state roots, assert selected backends
- **10x breakpoint**: inconsistent guard/helper code across resources would surface as composition-test failures before runtime scale becomes relevant

## Negative Tests

- **Malformed inputs**: mixed mode composition where one resource is SDK-backed and the others are in-memory must still resolve each factory independently
- **Error paths**: same-resource conflicts must continue to fail after mixed composition coverage is added
- **Boundary conditions**: the full suite should pass with no Azure, Docker, or emulator dependencies present

## Steps

1. Add `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs` to cover combinations such as Service Bus in-memory + Blob SDK + Key Vault in-memory.
2. Reconcile any shared registration-guard or helper code so the conflict semantics and missing-client diagnostics stay consistent across all three resources.
3. Extend the targeted resource test files only as needed to keep naming, helper usage, and negative coverage aligned with the final mixed-composition suite.
4. Run the complete solution test suite and use it as the final proof that the slice demo is true entirely inside `dotnet test`.

## Must-Haves

- [ ] Mixed-resource composition is covered by explicit xUnit assertions.
- [ ] Each resource can choose its backend independently in one service collection.
- [ ] Resource-specific conflict behavior remains intact after the shared test polish.
- [ ] `dotnet test Azure.InMemory.sln` is the final, infrastructure-free proof for S01.

## Verification

- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests`
- `dotnet test Azure.InMemory.sln`

## Observability Impact

- Signals added/changed: final suite-level assertions that identify the resource, selected backend, and conflicting registration path when composition breaks
- How a future agent inspects this: run `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests` or the full suite
- Failure state exposed: whether cross-resource composition regressed, which factory resolved incorrectly, and whether shared guards diverged across resources

## Inputs

- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — focused Service Bus proof from T02
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — focused Blob proof from T03
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — focused Key Vault proof from T04
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — Service Bus registration implementation to compose
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — Blob registration implementation to compose
- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` — Key Vault registration implementation to compose

## Expected Output

- `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs` — cross-resource composition proof
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — aligned Service Bus targeted coverage
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — aligned Blob targeted coverage
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — aligned Key Vault targeted coverage
