# S04: In-memory Key Vault basics

**Goal:** Close the roadmap lag around Key Vault basics by adding dedicated, behavior-focused proof that the existing in-memory Key Vault seam supports infrastructure-free secret set/get flows and stable test inspection.
**Demo:** After this: A test writes a secret and reads it back through the configured Key Vault factory with no external infrastructure.

## Tasks
- [x] **T01: Added a focused in-memory Key Vault behavior suite and trimmed the Key Vault registration tests back to DI-only coverage.** — Add focused Key Vault behavior proof outside the DI registration suite so S04 owns explicit evidence instead of relying on the S01 registration tests.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `InMemoryKeyVaultState` set/get and validation paths | Fail the focused test with assertions that name the exact secret scenario that regressed instead of masking it behind broader DI failures. | Not applicable; all operations are in-process and should complete immediately, so any hang indicates a regression in the test or state path. | Preserve the existing fail-fast contract for blank names and null values rather than silently normalizing invalid input. |
| `AddAzureKeyVaultInMemory()` resolution through `IAzureKeyVaultFactory` | Keep the task scoped to the existing DI seam; if factory resolution fails, the test should make that missing seam obvious before behavior assertions run. | Not applicable; factory resolution is synchronous in the test host. | Treat missing-state or wrong-backend behavior as a test failure rather than compensating in the suite. |

## Load Profile

- **Shared resources**: singleton `InMemoryKeyVaultState` reused by the factory and tests within one service provider.
- **Per-operation cost**: one in-memory dictionary read or write plus one returned record allocation per set/get.
- **10x breakpoint**: repeated overwrite scenarios would surface incorrect deduplication or stale-version behavior before any meaningful performance issue.

## Negative Tests

- **Malformed inputs**: blank or whitespace secret names and `null` secret values should fail fast.
- **Error paths**: missing-secret lookups must return `null` instead of throwing or synthesizing placeholder data.
- **Boundary conditions**: rewriting the same logical secret with different casing must preserve a single logical secret name while returning a new latest version.

## Steps

1. Create `tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs` with focused coverage for set/get through `IAzureKeyVaultFactory`, missing-secret null behavior, case-insensitive lookup, overwrite/latest-value semantics, fresh generated versions, and `InMemoryKeyVaultState` inspection helpers.
2. Narrow `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` to DI-focused coverage only, keeping backend selection, shared-state reuse, actionable missing-client diagnostics, and same-resource conflict behavior without burying slice proof there.
3. Run the focused Key Vault suite, then the broader Key Vault filter, then the full solution sequentially with `./Azure.InMemory.sln` to preserve the infrastructure-free `dotnet test` loop.

## Must-Haves

- [ ] `tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs` directly proves the in-memory Key Vault set/get demo through `IAzureKeyVaultFactory`.
- [ ] Focused tests cover missing-secret null behavior, case-insensitive lookup, overwrite/latest-value semantics, fresh generated versions, and fail-fast validation for invalid names or values.
- [ ] `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` remains DI-focused and no longer carries the primary slice proof burden.
- [ ] The Key Vault-focused and full-solution test runs pass sequentially with `./Azure.InMemory.sln`.
  - Estimate: 60m
  - Files: tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs, tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs
  - Verify: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryKeyVaultBehaviorTests`
`dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVault`
`dotnet test ./Azure.InMemory.sln`
