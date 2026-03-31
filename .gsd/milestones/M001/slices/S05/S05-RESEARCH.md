# M001/S05 — Research

**Date:** 2026-03-30

## Summary

S05 owns **R009** and supports **R003** plus the Blob side of **R010**. The key discovery is the same pattern S04 just closed for Key Vault: the roadmap demo for Blob basics is **already implemented and currently passing** from S01, but the proof still lives inside the DI registration suite instead of a dedicated Blob behavior suite.

The current codebase already provides:

- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — the stable public seam: `GetContainer(...)`, `GetBlobClient(...)`, `UploadAsync(...)`, `DownloadAsync(...)`, and `ExistsAsync(...)`.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs` — a deliberately thin adapter over shared state. Non-obvious but important: `GetContainer("name")` calls `EnsureContainer(...)`, so resolving a container establishes the in-memory namespace even before upload.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — the real in-memory behavior: case-insensitive container/blob dictionaries, cloned `BinaryData` on upload and download, `contentType` preservation, `overwrite` conflict handling, and state inspection via `ContainerNames`, `ContainerExists(...)`, `BlobExists(...)`, and `Download(...)`.
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — already proves the slice demo with `AddAzureBlobInMemorySupportsBasicUploadDownloadAndExistsFlowAgainstTheSharedState`, plus DI selection and diagnostics.

So S05 is **not** a greenfield implementation slice. It is a **verification-and-closeout** slice unless the planner intentionally re-scopes it. The highest-value move is to mirror S04’s structure: put runtime Blob proof in a dedicated behavior suite and keep registration tests DI-only.

The loaded `error-handling-patterns` skill is relevant here in two specific ways:

- **Fail Fast** — keep blank container/blob names and null content rejected at the existing state/factory boundary; do not silently normalize or auto-heal invalid inputs.
- **Preserve Context** — continue proving immutable/read-only snapshots through cloned `BinaryData` on state read/write instead of exposing mutable internals.

## Recommendation

Treat S05 as a **close-the-traceability-gap** slice, not a production rewrite.

### Preferred path

Add a dedicated Blob behavior suite and narrow the registration suite back to DI responsibilities.

Concretely:

1. Create `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` as the authoritative proof surface for R009.
2. Trim `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` so it focuses on:
   - SDK vs in-memory resolution
   - shared-state singleton reuse
   - missing `BlobServiceClient` diagnostics
   - same-resource conflict behavior
3. Avoid production code changes unless the focused tests uncover a real gap.

### What the dedicated Blob suite should prove first

The highest-value behavior proof, in order:

1. **Factory-driven round-trip through shared state** — upload, exists, download, preserved content type, and state inspection through `InMemoryBlobState`.
2. **Missing blob lookup returns null / false without synthesizing blob state** — mirrors the Key Vault “missing returns null” proof style and closes an important R009 edge.
3. **Overwrite semantics**
   - `overwrite: false` on an existing blob throws actionable `InvalidOperationException`
   - `overwrite: true` replaces content and `contentType`
4. **Case-insensitive logical identity** for container/blob names — the state dictionaries are already `OrdinalIgnoreCase`, but no test currently locks that down.
5. **Fail-fast validation** — blank container names, blank blob names, and null upload content.

### Optional proof if the planner wants to lock down a non-obvious convention

Because there is no explicit create-container API in the public seam, `GetContainer("reports")` currently establishes the container namespace via `EnsureContainer(...)`. That behavior is documented in `.gsd/KNOWLEDGE.md` and may be worth an explicit test if the slice wants to make the convention durable.

### What not to do

- Do not widen `IAzureBlobFactory` for M001.
- Do not chase Blob SDK parity beyond basic upload/download/exists.
- Do not add a broad new inspection API unless the dedicated tests prove the existing `InMemoryBlobState` surface is insufficient.

## Implementation Landscape

### Key files

- `.gsd/milestones/M001/slices/S05/S05-PLAN.md` — currently has only the goal/demo header and no tasks; planning should start by acknowledging this is now a closeout slice.
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — stable public contract sized correctly for M001; no obvious gap.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs` — thin in-memory adapter; should stay thin.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — natural seam for any remaining work. This is where behavior already lives and where any narrowly-scoped inspection/helper change would belong.
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — already complete for backend registration, conflict guards, and missing-client diagnostics.
- `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs` — useful semantic reference: `DownloadAsync()` returns `null` when the blob is absent, and upload honors overwrite/content-type intent.
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — currently mixes DI proof with runtime Blob behavior proof; this is the main test file to split conceptually.
- `tests/Azure.InMemory.Tests/Blob/InMemory/` — currently **absent**. This is the missing focused proof location.

### What already exists

- `InMemoryBlobState` stores containers and blobs in case-insensitive dictionaries.
- `Upload(...)` clones incoming `BinaryData` and preserves `contentType`.
- `Download(...)` returns a cloned `BinaryData` snapshot, not the stored instance.
- Duplicate upload without overwrite already throws an actionable `InvalidOperationException` naming the blob and container and instructing the caller to pass `overwrite: true`.
- `GetContainer(...)` ensures the container exists in state even before any blob upload.

### Real gaps if S05 stays open

1. **No dedicated Blob behavior test area** — unlike Service Bus and now Key Vault, Blob runtime proof is still buried in DI registration tests.
2. **No explicit overwrite test coverage** — current behavior exists in code, but the suite does not lock down duplicate vs overwrite semantics.
3. **No explicit missing-blob behavior proof** — `DownloadAsync()`/`ExistsAsync()` behavior for absent blobs is implemented but not directly asserted in a focused suite.
4. **No explicit case-insensitive behavior proof** — container/blob state is case-insensitive by implementation, but the contract is not yet pinned by tests.

### Natural seam / build order

1. **Write the focused Blob behavior tests first** in `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs`.
2. **Move or trim the runtime Blob assertions out of `BlobProviderRegistrationTests.cs`** so the DI suite stays composition-focused.
3. **Only then touch production code** if a focused behavior test exposes a real mismatch between intended and current behavior.
4. Run verification **sequentially**, not in parallel.

## Constraints / Notes

- S05’s original roadmap demo is already satisfied; new work must justify itself as proof-location cleanup, hardening, or narrow observability.
- Per `.gsd/KNOWLEDGE.md`, the Blob seam intentionally has no explicit container-creation API; tests should respect that `GetContainer(...)` is the namespace-establishing action in in-memory mode.
- Per the loaded `error-handling-patterns` skill, preserve fail-fast validation and meaningful exceptions rather than auto-creating around invalid input.
- No Context7/library-doc lookup was needed: `Azure.Storage.Blobs` is already in active use and the codebase contains the relevant semantic reference.
- Sequential verification matters. The project knowledge log already records transient `MSB3026` copy-retry warnings when multiple `dotnet test` commands overlap in the same worktree.

## Skill Discovery Suggestions

No installed skill from `<available_skills>` directly targets **Azure Blob Storage for .NET**.

External skill discovery results worth noting:

- Query: `.NET xUnit integration testing`
  - Promising: `npx skills add novotnyllc/dotnet-artisan@dotnet-testing` — highest install count among relevant .NET testing skills returned and directly aligned with the work being done here.
  - Secondary: `npx skills add claude-dev-suite/claude-dev-suite@xunit` — narrower, but relevant if the team wants xUnit-specific test-structure guidance.
- Query: `Azure Blob Storage .NET testing`
  - No strong .NET Blob skill surfaced. The highest Azure Blob results were Python-focused (`azure-storage-blob-py`) or unrelated Azure SDK domains, so I would **not** recommend them for S05.

## Verification

Current baseline in this worktree is green:

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~Blob` → ✅ pass (10 tests)
- `dotnet test ./Azure.InMemory.sln` → ✅ pass (57 tests)

For the execution slice, the verification ladder should mirror S04:

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryBlobBehaviorTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~Blob`
- `dotnet test ./Azure.InMemory.sln`
