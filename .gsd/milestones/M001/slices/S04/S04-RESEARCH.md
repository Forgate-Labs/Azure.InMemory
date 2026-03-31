# M001/S04 — Research

**Date:** 2026-03-30

## Summary

S04 nominally owns **R008** and supports **R003** plus the Key Vault side of **R010**. The important discovery is that the roadmap demo for this slice is **already implemented and passing** from S01. The current codebase already provides:

- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — a deliberately narrow seam with `GetSecretStore()`, `SetSecretAsync(...)`, and `GetSecretAsync(...)`.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs` — a thin in-memory adapter that delegates straight to shared state.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs` — real in-memory behavior for set/get, case-insensitive secret storage, generated versions, `SecretExists(...)`, and `SecretNames`.
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — already proves the exact slice demo with `AddAzureKeyVaultInMemorySupportsBasicSetAndGetFlowAgainstTheSharedState`.

So S04 is **not** a greenfield implementation slice anymore. It is a roadmap lag / traceability lag: the code and tests already satisfy the original “write a secret and read it back with no external infrastructure” outcome, but the dedicated S04 plan file still has no tasks.

Two practical consequences follow:

1. If the milestone wants to stay strict to the original slice boundaries, S04 should be treated as a **verification-and-closeout** slice.
2. If the milestone wants every remaining slice to add net-new value, S04 should be **re-scoped** to Key Vault hardening / observability rather than re-implementing basics.

The loaded `error-handling-patterns` skill is the only directly useful installed guidance here. If S04 is re-scoped, its **Fail Fast** and **Preserve Context** rules fit the existing design: keep validation at the state/factory boundary, and expose read-only test evidence instead of leaking mutable internals.

## Recommendation

Treat S04 as a **decision-first** slice.

### Preferred path

Re-scope S04 to **“Key Vault hardening and observability”** while keeping the public factory seam unchanged.

That means:

- do **not** widen `IAzureKeyVaultSecretStore` beyond basic set/get for M001;
- keep behavior/state ownership in `InMemoryKeyVaultState`, matching the Blob and Service Bus pattern;
- add focused Key Vault behavior tests outside the DI registration suite;
- only add narrow state inspection helpers if the new tests need proof beyond `GetSecret(...)`, `SecretExists(...)`, and `SecretNames`.

### Minimal path

If roadmap discipline matters more than adding new scope, make S04 a **closure slice**:

- add a dedicated Key Vault behavior test file so the requirement proof is not buried in registration tests;
- run focused + full verification;
- record R008/R003 evidence and complete the slice without changing the public API.

### What not to do

- Do not rebuild the current set/get path; it already works.
- Do not introduce broader Key Vault SDK parity for M001.
- Do not move observability into the public factory seam when `InMemoryKeyVaultState` is already the established test surface.

## Implementation Landscape

### Key Files

- `.gsd/milestones/M001/slices/S04/S04-PLAN.md` — currently only has the goal/demo header and **no tasks**. The first planning action should be deciding whether S04 is closeout-only or re-scoped.
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — stable public contract. Good sign: it is already sized exactly to M001 basics.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultFactory.cs` — intentionally thin adapter. This should stay thin.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs` — the natural implementation seam for any remaining S04 work. It currently owns actual secret storage and the only test-oriented inspection helpers.
- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` — registration is already done and should likely stay untouched unless the slice becomes pure documentation/verification cleanup.
- `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs` — reference for the SDK-backed shape; useful if tests need to preserve semantics like “missing secret returns null”.
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — current proof for the happy path, missing-secret null behavior, generated version, singleton state reuse, missing-client diagnostics, and backend-conflict behavior.
- `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs` — guardrail proving Key Vault backend choice composes with Service Bus and Blob.

### What Already Exists

- `InMemoryKeyVaultState` stores secrets in a `ConcurrentDictionary<string, ...>(StringComparer.OrdinalIgnoreCase)`, so lookups are already case-insensitive.
- `SetSecret(...)` generates a synthetic version (`Guid.NewGuid().ToString("N")`) on every write.
- `GetSecret(...)` returns a copy of the stored record, not the backing entry instance.
- `InMemoryKeyVaultFactory` honors cancellation before delegating to state.
- SDK-backed Key Vault behavior already maps 404 to `null`, which aligns the seam with in-memory missing-secret behavior.

### Real Gaps If S04 Stays Open

These are the only meaningful gaps left for S04-level work:

1. **No dedicated Key Vault behavior test area** — Key Vault proof currently lives inside DI registration tests, unlike the later Service Bus slices which gained focused behavioral suites.
2. **No explicit proof for repeated writes** — the implementation obviously overwrites the latest value and generates a new version, but there is no dedicated test locking that behavior down.
3. **Limited public observability** — `SecretExists(...)` and `SecretNames` exist, but the stored `UpdatedAt` is private and there is no richer read-only inspection surface if tests later need more than latest-value proof.
4. **Only latest-version storage exists** — generated versions are returned, but prior versions are discarded. That is acceptable for M001 basics, but it means any future “version history” scope would require a real state change.

### Natural Seam / Build Order

1. **Decide slice scope first** — closure-only vs re-scoped hardening.
2. **If re-scoped, write focused failing tests first** in a dedicated Key Vault behavior file (for example under `tests/Azure.InMemory.Tests/KeyVault/InMemory/`).
3. **Only then extend `InMemoryKeyVaultState`** with the narrowest inspection helpers required by those tests.
4. **Keep `InMemoryKeyVaultFactory` thin** and avoid changing `IAzureKeyVaultFactory` unless a truly unavoidable contract gap appears.
5. **Run verification sequentially** with `./Azure.InMemory.sln`.

## Constraints / Notes

- S04’s original roadmap demo is already satisfied; any new work must justify itself as hardening, observability, or traceability.
- `InMemoryKeyVaultState` currently stores only the **latest** secret record per name. Do not accidentally promise version-history fidelity without implementing it.
- The private `UpdatedAt` field is a strong hint that richer inspection was considered, but it is not currently usable by tests.
- The `error-handling-patterns` skill should guide any remaining work:
  - **Fail Fast** — keep blank-name / null-value validation at the existing boundaries.
  - **Preserve Context** — if observability grows, return immutable snapshots/records rather than mutable internal structures.
- I verified the current slice proof and the full infrastructure-free loop locally. One run emitted a transient `MSB3026` copy warning when two test commands overlapped, so planners should keep verification commands **sequential**, not parallel.

## Skill Discovery Suggestions

No installed skill from `<available_skills>` directly targets .NET Key Vault Secrets work.

One external skill search was run:

- Query: `Azure SDK for .NET Key Vault xUnit`
- Result found: `npx skills add sickn33/antigravity-awesome-skills@azure-security-keyvault-keys-dotnet`

This is **not a strong recommendation** for S04 because it targets **Key Vault Keys**, while this slice uses **Key Vault Secrets**. I would only consider it if later milestone scope expands into broader Azure security SDK patterns.

## Verification

Already confirmed in the current worktree:

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests` → ✅ pass (5 tests)
- `dotnet test ./Azure.InMemory.sln` → ✅ pass (48 tests)

If S04 is re-scoped into dedicated Key Vault hardening, use:

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVault`
- `dotnet test ./Azure.InMemory.sln`
