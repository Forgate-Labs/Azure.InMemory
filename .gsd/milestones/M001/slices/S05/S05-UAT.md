# S05: In-memory Blob basics — UAT

**Milestone:** M001
**Written:** 2026-03-30T22:48:39.384Z

# S05: In-memory Blob basics — UAT

**Milestone:** M001
**Written:** 2026-03-30

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S05 ships in-process Blob behavior proof and DI test refocusing only; the authoritative user-visible outcome is a green `dotnet test` loop that exercises Blob write/read behavior through `IAzureBlobFactory` with no external infrastructure.

## Preconditions

- Run from the repository root in this worktree.
- .NET SDK for the solution is installed.
- No Azure account, storage emulator, Docker container, or other external infrastructure is required.
- Execute verification commands sequentially against `./Azure.InMemory.sln`.

## Smoke Test

Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryBlobBehaviorTests` and confirm the focused in-memory Blob behavior suite passes.

## Test Cases

### 1. Blob round trip through the in-memory factory

1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryBlobBehaviorTests`.
2. Confirm the focused suite passes the cases that create a container via `GetContainer(...)`, upload blob content through `IAzureBlobFactory`, and download it again.
3. **Expected:** The suite stays green, proving an in-memory blob can be written and read back through the factory seam, `ExistsAsync` reports the blob as present, and `contentType` is preserved with the stored payload.

### 2. Missing blob and overwrite semantics stay truthful

1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~Blob`.
2. Confirm the Blob-focused run includes passing coverage for missing-blob lookups and duplicate uploads.
3. **Expected:** Missing blobs resolve to `false`/`null`, duplicate uploads without `overwrite: true` fail with an actionable `InvalidOperationException`, and overwrite-enabled uploads replace the stored payload instead of silently duplicating it.

### 3. Infrastructure-free regression loop stays green

1. Run `dotnet test ./Azure.InMemory.sln`.
2. Confirm the full solution passes after the focused Blob runs.
3. **Expected:** The complete repository remains green under a single in-process `dotnet test` loop, showing the Blob proof extraction did not break broader Service Bus, Key Vault, or DI behavior.

## Edge Cases

### Case-insensitive identity and cloned download snapshots

1. Use the focused Blob suite run to confirm the case-insensitive container/blob lookup and cloned snapshot coverage passes.
2. **Expected:** Rewriting the same logical blob with different casing still targets one logical identity, and downloaded `BinaryData` snapshots remain stable instead of exposing shared mutable state.

### Invalid names and null payloads fail fast

1. Use the focused Blob suite run to confirm validation coverage for blank container names, blank blob names, and `null` content passes.
2. **Expected:** Invalid inputs fail immediately rather than being normalized or accepted silently.

## Failure Signals

- Any failure in `InMemoryBlobBehaviorTests` for upload/download/exists, missing-blob, overwrite, casing, or validation scenarios.
- Any failing `FullyQualifiedName~Blob` regression test after the DI/runtime proof split.
- Any full-solution `dotnet test ./Azure.InMemory.sln` failure, which would indicate the Blob slice regressed the infrastructure-free loop.

## Not Proven By This UAT

- Real Azure Storage service behavior, network I/O, authentication, or production account integration.
- Advanced Blob fidelity beyond the M001 basics, including Azure Functions blob-trigger integration and richer storage semantics deferred to later milestones.

## Notes for Tester

Run the verification commands sequentially, not in parallel, to avoid shared `bin/obj` contention noise in this solution. If the focused Blob suite fails, start there before looking at the broader Blob filter or full-solution run because it gives the clearest behavior-level diagnostics for this slice.
