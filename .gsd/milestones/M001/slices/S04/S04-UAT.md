# S04: In-memory Key Vault basics — UAT

**Milestone:** M001
**Written:** 2026-03-30T22:31:14.462Z

# S04: In-memory Key Vault basics — UAT

**Milestone:** M001  
**Written:** 2026-03-30

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: This slice delivers library behavior proved entirely by automated tests inside `dotnet test`; there is no long-running runtime or external Key Vault environment to inspect manually.

## Preconditions

- Work from the repository root in this worktree.
- Use the explicit solution path `./Azure.InMemory.sln`.
- Have the .NET SDK installed and avoid competing parallel `dotnet test` runs against the same worktree.

## Smoke Test

Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryKeyVaultBehaviorTests`.

Expected: the focused Key Vault behavior suite passes and reports the round-trip, missing-secret, overwrite/latest-version, and invalid-input tests green.

## Test Cases

### 1. Secret round-trip and shared-state proof

1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryKeyVaultBehaviorTests`.
2. Confirm the suite includes and passes `SecretStoreRoundTripsASecretThroughTheResolvedFactoryAndSharedState`.
3. Confirm the same run also passes `MissingSecretLookupReturnsNullWithoutSynthesizingPlaceholderState`.
4. **Expected:** The in-memory Key Vault seam proves a secret can be written and read back through `IAzureKeyVaultFactory`, shared `InMemoryKeyVaultState` starts empty and then shows the stored logical secret, and missing lookups return `null` instead of placeholder data or exceptions.

### 2. Latest-value and case-insensitive overwrite behavior

1. Reuse the focused behavior-suite run above or run it again.
2. Confirm `OverwritingTheSameLogicalSecretIsCaseInsensitiveAndReturnsANewLatestVersion` passes.
3. **Expected:** Rewriting the same logical secret with different casing produces a fresh version, preserves one logical secret name in shared state, and returns the newest value when retrieved.

### 3. DI coverage still stays separate and green

1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVault`.
2. Confirm the run passes both the in-memory behavior suite and `KeyVaultProviderRegistrationTests`.
3. **Expected:** Key Vault behavior proof and Key Vault DI registration coverage both stay green together, with registration tests limited to backend resolution, shared-state reuse, missing-`SecretClient` diagnostics, and same-resource conflict handling.

## Edge Cases

### Invalid secret inputs fail fast

1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryKeyVaultBehaviorTests`.
2. Confirm `GetSecretRejectsBlankSecretNames`, `SetSecretRejectsBlankSecretNames`, and `SetSecretRejectsNullSecretValues` all pass.
3. **Expected:** Blank or whitespace secret names throw argument exceptions, `null` values throw `ArgumentNullException`, and the in-memory store does not silently normalize malformed input.

## Failure Signals

- The focused Key Vault behavior-suite command fails or reports fewer expected tests.
- The `FullyQualifiedName~KeyVault` regression fails, which usually means behavior proof and DI coverage drifted out of sync.
- The full solution run (`dotnet test ./Azure.InMemory.sln`) regresses after Key Vault-specific commands pass, indicating slice-local changes broke another area.

## Not Proven By This UAT

- Real Azure Key Vault connectivity or `SecretClient` behavior against live infrastructure.
- Advanced Key Vault semantics beyond M001 basics, such as delete/recover, tags/content types, or explicit retrieval of older secret versions.

## Notes for Tester

Run the commands sequentially, not in parallel, because this solution shares `bin/obj` outputs and parallel test runs can produce transient copy-retry noise. Use the explicit relative solution path `./Azure.InMemory.sln`.
