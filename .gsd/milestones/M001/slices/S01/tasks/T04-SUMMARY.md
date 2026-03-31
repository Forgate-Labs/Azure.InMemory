---
id: T04
parent: S01
milestone: M001
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs", "src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs", "src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs", "src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs", "tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Resolved the SDK-backed Key Vault factory through a DI activation lambda so missing SecretClient dependencies fail when IAzureKeyVaultFactory is resolved, with actionable AddAzureKeyVaultSdk() guidance.", "Made the in-memory Key Vault backend truthful for the planned S04 seam by storing latest secret values plus generated versions in a singleton InMemoryKeyVaultState instead of leaving registration-only stubs."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests and dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests.Conflicting; both passed and proved SDK vs in-memory resolution, singleton InMemoryKeyVaultState reuse, actionable missing SecretClient failures, and fail-fast conflicting Key Vault registration behavior. Also ran dotnet test ./Azure.InMemory.sln as an early slice-level check; it still fails only because MixedProviderCompositionTests remains the intentional T05 placeholder."
completed_at: 2026-03-30T21:02:01.854Z
blocker_discovered: false
---

# T04: Implemented Key Vault SDK and in-memory provider registration with shared state and focused provider tests.

> Implemented Key Vault SDK and in-memory provider registration with shared state and focused provider tests.

## What Happened
---
id: T04
parent: S01
milestone: M001
key_files:
  - src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs
  - src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs
  - src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs
  - src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Resolved the SDK-backed Key Vault factory through a DI activation lambda so missing SecretClient dependencies fail when IAzureKeyVaultFactory is resolved, with actionable AddAzureKeyVaultSdk() guidance.
  - Made the in-memory Key Vault backend truthful for the planned S04 seam by storing latest secret values plus generated versions in a singleton InMemoryKeyVaultState instead of leaving registration-only stubs.
duration: ""
verification_result: mixed
completed_at: 2026-03-30T21:02:01.856Z
blocker_discovered: false
---

# T04: Implemented Key Vault SDK and in-memory provider registration with shared state and focused provider tests.

**Implemented Key Vault SDK and in-memory provider registration with shared state and focused provider tests.**

## What Happened

Kept IAzureKeyVaultFactory unchanged because its existing GetSecretStore() seam already matched the planned S04 SetSecret/GetSecret scope. Added AzureKeyVaultSdkFactory as a thin adapter over a DI-registered SecretClient, including narrow 404 handling so GetSecretAsync() returns null through the library seam. Added InMemoryKeyVaultFactory and singleton InMemoryKeyVaultState so the in-memory backend already supports truthful basic set/get behavior with generated secret versions. Added AddAzureKeyVaultSdk() and AddAzureKeyVaultInMemory() with the same explicit conflict-guard pattern used by the other resources, replaced the placeholder Key Vault registration tests with focused coverage, and documented the infrastructure-free SecretClient test setup pattern in .gsd/KNOWLEDGE.md.

## Verification

Ran dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests and dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests.Conflicting; both passed and proved SDK vs in-memory resolution, singleton InMemoryKeyVaultState reuse, actionable missing SecretClient failures, and fail-fast conflicting Key Vault registration behavior. Also ran dotnet test ./Azure.InMemory.sln as an early slice-level check; it still fails only because MixedProviderCompositionTests remains the intentional T05 placeholder.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests` | 0 | ✅ pass | 3687ms |
| 2 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests.Conflicting` | 0 | ✅ pass | 3641ms |
| 3 | `dotnet test ./Azure.InMemory.sln` | 1 | ❌ fail | 3325ms |


## Deviations

None. The existing Key Vault factory contract already matched the planned S04 secret seam, so no public contract change was necessary.

## Known Issues

The full-solution verification loop still fails because tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs remains an intentional placeholder for T05. No new blocker was discovered for T04.

## Files Created/Modified

- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs`
- `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs`
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs`
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
None. The existing Key Vault factory contract already matched the planned S04 secret seam, so no public contract change was necessary.

## Known Issues
The full-solution verification loop still fails because tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs remains an intentional placeholder for T05. No new blocker was discovered for T04.
