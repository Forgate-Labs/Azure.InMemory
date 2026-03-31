---
id: S05
parent: M001
milestone: M001
provides:
  - Authoritative in-memory Blob behavior proof through `AddAzureBlobInMemory()` and `IAzureBlobFactory`, with Blob registration coverage narrowed to DI-only seam validation.
requires:
  - slice: S01
    provides: The explicit Blob registration seam, `IAzureBlobFactory`, and shared `InMemoryBlobState` implementation that S05 could verify without widening the public API.
affects:
  []
key_files:
  - tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
key_decisions:
  - Moved Blob runtime proof into a focused in-memory behavior suite and kept BlobProviderRegistrationTests limited to backend registration and misconfiguration coverage.
patterns_established:
  - When a resource's basic runtime behavior already exists behind the seam, extract that proof into a dedicated `InMemory...BehaviorTests` suite and keep provider registration tests limited to DI composition, shared-state reuse, and actionable misconfiguration diagnostics.
observability_surfaces:
  - Focused `InMemoryBlobBehaviorTests` now provide an authoritative, low-noise proof surface for Blob round trips, missing-blob behavior, overwrite semantics, casing rules, and cloned snapshot behavior through `IAzureBlobFactory`.
drill_down_paths:
  - .gsd/milestones/M001/slices/S05/tasks/T01-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-30T22:48:39.378Z
blocker_discovered: false
---

# S05: In-memory Blob basics

**Dedicated in-memory Blob behavior tests now prove blob write/read basics through `IAzureBlobFactory`, while Blob registration tests stay focused on DI wiring and diagnostics.**

## What Happened

S05 closed the roadmap traceability gap for Blob basics without widening the public Blob seam. The slice added `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` as the authoritative runtime proof surface for `AddAzureBlobInMemory()` and `IAzureBlobFactory`, covering the full blob MVP contract: upload/download/exists round trips, missing-blob `false`/`null` behavior, duplicate upload diagnostics versus overwrite replacement, case-insensitive container/blob lookup, cloned input and download snapshots, preserved `contentType`, fail-fast validation for blank names and `null` content, and the `GetContainer(...)` namespace-establishing convention. In parallel, `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` was trimmed back to DI-only responsibilities so backend selection, shared-state reuse, missing-client diagnostics, and conflicting registration behavior remain easy to diagnose without carrying the primary runtime proof burden. The result is the same pattern already established for Key Vault: resource behavior proof lives in a dedicated in-memory behavior suite, while provider registration tests stay focused on composition and diagnostics.

## Verification

Executed the slice verification plan sequentially with `dotnet test` only: `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryBlobBehaviorTests` (12 passed, 0 failed, 4317 ms), `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~Blob` (21 passed, 0 failed, 3659 ms), and `dotnet test ./Azure.InMemory.sln` (68 passed, 0 failed, 3643 ms). These runs verify the dedicated Blob behavior suite, the broader Blob regression surface, and the full infrastructure-free solution loop.

## Requirements Advanced

None.

## Requirements Validated

- R009 — `InMemoryBlobBehaviorTests` now prove upload/download/exists through `AddAzureBlobInMemory()` and `IAzureBlobFactory`, including missing-blob `false`/`null` behavior, overwrite conflict vs replacement semantics, case-insensitive container/blob lookup, cloned content snapshots, preserved `contentType`, and the `GetContainer(...)` namespace-establishing convention, with the focused Blob suite, broader Blob filter, and full solution all passing inside `dotnet test`.
- R003 — With S05 closing the final Blob proof gap, the supported M001 scenarios now remain green in-process under sequential `dotnet test` runs: the focused Blob suite, Blob-filtered regression run, and full `./Azure.InMemory.sln` run all pass without Azure, Docker, or other external infrastructure.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

Blob support in M001 remains intentionally scoped to basic in-memory read/write semantics. Advanced Azure Storage fidelity, richer concurrency semantics, and Azure Functions blob-trigger integration remain deferred beyond this milestone.

## Follow-ups

None.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` — Added the dedicated in-memory Blob behavior suite that now serves as the authoritative proof surface for factory-driven blob basics, including round trips, missing blobs, overwrite behavior, case-insensitive identity, cloned snapshots, preserved content type, fail-fast validation, and container namespace establishment.
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — Trimmed Blob registration coverage back to DI-only concerns such as backend selection, shared-state reuse, missing BlobServiceClient diagnostics, and conflicting same-resource registrations.
- `.gsd/KNOWLEDGE.md` — Appended downstream guidance capturing that Blob runtime proof now lives in the dedicated behavior suite and that Blob registration tests should remain DI-focused.
- `.gsd/PROJECT.md` — Refreshed the project status summary to reflect that S05 is now implemented and verified and that Blob basics are proven by the dedicated in-memory behavior suite.
