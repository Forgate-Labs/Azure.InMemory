---
id: S04
parent: M001
milestone: M001
provides:
  - Dedicated Key Vault behavior proof for infrastructure-free secret set/get through IAzureKeyVaultFactory.
  - A repeatable testing pattern that separates runtime behavior coverage from provider-registration coverage for resource slices.
requires:
  - slice: S01
    provides: The explicit AddAzureKeyVaultInMemory()/AddAzureKeyVaultSdk() seam, focused IAzureKeyVaultFactory contract, and shared InMemoryKeyVaultState-backed secret-store implementation established in S01.
affects:
  - S05
key_files:
  - tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
  - .gsd/DECISIONS.md
key_decisions:
  - Dedicated Key Vault runtime proof now lives in InMemoryKeyVaultBehaviorTests, while KeyVaultProviderRegistrationTests remains DI-only.
  - R008 is now validated by behavior-focused proof through IAzureKeyVaultFactory instead of relying on registration tests as the primary evidence surface.
patterns_established:
  - Use dedicated resource behavior suites as the authoritative capability proof surface, and keep provider registration tests limited to DI composition and diagnostics.
  - Treat shared in-memory state as a stable test inspection surface alongside factory-driven behavior assertions.
observability_surfaces:
  - Shared InMemoryKeyVaultState inspection via SecretExists and SecretNames gives tests a stable way to confirm logical secret presence and naming after factory-driven operations.
drill_down_paths:
  - .gsd/milestones/M001/slices/S04/tasks/T01-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-30T22:31:14.455Z
blocker_discovered: false
---

# S04: In-memory Key Vault basics

**Dedicated in-memory Key Vault behavior tests now prove factory-based secret set/get with no external infrastructure, while Key Vault registration tests stay focused on DI wiring and diagnostics.**

## What Happened

S04 closed the Key Vault traceability gap without widening the public seam. The slice added InMemoryKeyVaultBehaviorTests as the primary proof surface for AddAzureKeyVaultInMemory() and IAzureKeyVaultFactory, so the roadmap demo is now exercised the same way consumers use the library: resolve the factory, set a secret, read it back, and inspect the shared InMemoryKeyVaultState. The suite explicitly proves missing-secret lookups return null, secret names are treated case-insensitively, overwriting the same logical secret creates a fresh latest version, and blank names or null values fail fast instead of being silently normalized.

At the same time, KeyVaultProviderRegistrationTests was narrowed back to DI-only responsibilities. It still verifies SDK vs in-memory backend selection, singleton shared-state reuse, actionable missing-SecretClient diagnostics, and same-resource conflict behavior, but it no longer carries the primary runtime behavior burden. This keeps Key Vault proof layered the same way the Service Bus slices structured capability proof versus registration composition coverage.

## Verification

Ran the full slice verification ladder sequentially against ./Azure.InMemory.sln and all commands passed: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryKeyVaultBehaviorTests` (10 tests, 4052 ms wall time), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVault` (19 tests, 3472 ms wall time), and `dotnet test ./Azure.InMemory.sln` (57 tests, 3408 ms wall time). This verifies the dedicated Key Vault behavior suite, the DI-only Key Vault registration coverage, and overall solution compatibility together in the infrastructure-free dotnet test loop.

## Requirements Advanced

- R003 — Added dedicated infrastructure-free Key Vault behavior proof inside dotnet test, narrowing the remaining R003 gap to Blob basics only.

## Requirements Validated

- R008 — InMemoryKeyVaultBehaviorTests proves SetSecretAsync/GetSecretAsync through AddAzureKeyVaultInMemory() and IAzureKeyVaultFactory, including missing-secret null behavior, case-insensitive overwrite/latest-version semantics, shared-state inspection, and fail-fast invalid-input checks; focused, KeyVault-wide, and full-solution dotnet test runs all passed.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

M001 Key Vault coverage remains intentionally narrow: this slice proves only basic SetSecretAsync/GetSecretAsync behavior, shared-state inspection, and validation paths. Advanced Key Vault semantics such as delete/recover, tags/content types, and version-specific retrieval beyond latest-value behavior remain out of scope for this milestone.

## Follow-ups

S05 should mirror this proof structure for Blob basics: keep provider registration tests DI-focused and place runtime blob behavior proof in a dedicated in-memory behavior suite.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs` — Added the authoritative S04 behavior suite for factory-based secret round-trip, missing-secret null behavior, case-insensitive overwrite/latest-version semantics, and fail-fast validation.
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — Trimmed the Key Vault registration suite back to backend selection, shared-state reuse, missing-client diagnostics, and conflict detection.
- `.gsd/KNOWLEDGE.md` — Recorded the dedicated Key Vault proof location and the non-obvious case-insensitive/latest-version behavior contract for future slices.
- `.gsd/PROJECT.md` — Refreshed milestone state to reflect S04 completion and the remaining Blob-only gap.
- `.gsd/DECISIONS.md` — Recorded the new Key Vault proof-location decision and the requirement-validation decision for R008.
