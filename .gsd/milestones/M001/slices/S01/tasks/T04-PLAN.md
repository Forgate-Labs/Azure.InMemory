---
estimated_steps: 5
estimated_files: 6
skills_used:
  - error-handling-patterns
---

# T04: Implement Key Vault SDK and in-memory provider registration

**Slice:** S01 — Provider registration and focused factories
**Milestone:** M001

## Description

Implement the Key Vault registration seam using the same explicit provider-selection pattern as the other resources, sized only for the basic S04 `SetSecret` / `GetSecret` path. This task completes the third focused factory family and proves that Key Vault can switch between SDK and in-memory backends while surfacing stable, test-friendly registration behavior.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| DI-registered `SecretClient` | Throw an actionable `InvalidOperationException` that names the missing Key Vault SDK client dependency. | Not applicable at execution time; S01 registration must stay in-process and avoid network work. | Not applicable yet; Key Vault registration should not parse remote payloads in S01. |
| Resource registration mode selection | Throw an actionable `InvalidOperationException` when both SDK and in-memory Key Vault registrations are applied to the same service collection. | Not applicable; registration is synchronous. | Not applicable; invalid state is modeled as conflicting registrations. |

## Load Profile

- **Shared resources**: singleton `InMemoryKeyVaultState` and DI container lifetime graph
- **Per-operation cost**: service registration plus service-provider construction; no secret-store I/O in S01 tests
- **10x breakpoint**: duplicated state-root registrations or inconsistent guard behavior would appear as wrong-type or wrong-instance assertions long before runtime load matters

## Negative Tests

- **Malformed inputs**: duplicate same-resource registration by calling both `AddAzureKeyVaultSdk()` and `AddAzureKeyVaultInMemory()`
- **Error paths**: SDK mode without the required `SecretClient` registration should fail clearly
- **Boundary conditions**: repeated resolution of the in-memory backend should reuse the same `InMemoryKeyVaultState`

## Steps

1. Refine `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` so it stays focused on the basic secret set/get operations S04 will implement.
2. Implement `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs` as a thin adapter over a DI-registered `SecretClient`.
3. Implement `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs` and `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs`, registering the state as a singleton root for later Key Vault behavior.
4. Add `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` with `AddAzureKeyVaultSdk()` and `AddAzureKeyVaultInMemory()` plus the same explicit conflict-guard pattern.
5. Add `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` covering SDK selection, in-memory selection, shared state reuse, and conflicting-registration failure behavior.

## Must-Haves

- [ ] `IAzureKeyVaultFactory` stays focused on the S04 secret seam.
- [ ] `AddAzureKeyVaultSdk()` binds to a DI-registered `SecretClient`.
- [ ] `AddAzureKeyVaultInMemory()` exposes a singleton `InMemoryKeyVaultState`.
- [ ] Conflicting same-resource registrations fail fast with actionable text.
- [ ] Targeted Key Vault registration tests pass.

## Verification

- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests`
- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests.Conflicting`

## Observability Impact

- Signals added/changed: explicit conflict and missing-client exception messages plus assertions on resolved implementation types and singleton key-vault-state identity
- How a future agent inspects this: run `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests`
- Failure state exposed: which Key Vault registration path failed, whether the wrong backend resolved, and whether the key-vault state root stopped being shared

## Inputs

- `src/Azure.InMemory/Azure.InMemory.csproj` — library package references and assembly identity from T01
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — test framework and project reference from T01
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — base Key Vault seam to implement
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — prior art for the resource-specific guard pattern
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — prior art for consistency with Blob wiring
- `.gsd/milestones/M001/slices/S01/S01-RESEARCH.md` — Key Vault seam scope and DI guidance

## Expected Output

- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` — Key Vault registration extensions and guards
- `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs` — SDK-backed Key Vault adapter
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs` — in-memory Key Vault factory
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs` — singleton in-memory key-vault state root
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — focused Key Vault registration tests
