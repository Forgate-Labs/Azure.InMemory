---
estimated_steps: 3
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T01: Add a dedicated in-memory Blob behavior suite and close the slice with sequential Blob regressions

**Slice:** S05 — In-memory Blob basics
**Milestone:** M001

## Description

Add focused Blob behavior proof outside the DI registration suite so S05 owns explicit evidence for the in-memory Blob MVP instead of relying on S01's registration tests.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `InMemoryBlobState` upload/download/validation paths | Fail the focused test with assertions that name the exact Blob scenario that regressed instead of masking it behind broader DI failures. | Not applicable; all operations are in-process and should complete immediately, so any hang is a regression in the test or state path. | Preserve the existing fail-fast contract for blank names and `null` content rather than silently normalizing invalid input. |
| `AddAzureBlobInMemory()` resolution through `IAzureBlobFactory` | Keep the task scoped to the existing DI seam; if factory resolution fails, the test should expose that before behavior assertions run. | Not applicable; factory resolution is synchronous in the test host. | Treat wrong-backend or missing-state behavior as a test failure rather than compensating in the suite. |

## Load Profile

- **Shared resources**: singleton `InMemoryBlobState` reused by the factory and tests within one service provider
- **Per-operation cost**: one in-memory dictionary lookup/write plus cloned `BinaryData` allocation per upload/download
- **10x breakpoint**: repeated overwrite and download snapshot scenarios would expose stale content, lost `contentType`, or shared-mutable-state bugs before any meaningful performance issue

## Negative Tests

- **Malformed inputs**: blank or whitespace container/blob names and `null` upload content should fail fast
- **Error paths**: missing blobs must return `false`/`null`, and duplicate uploads without `overwrite: true` must raise an actionable `InvalidOperationException`
- **Boundary conditions**: rewriting the same logical blob with different casing should preserve one logical blob identity, and `GetContainer(...)` should establish the in-memory container namespace before upload

## Steps

1. Create `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` with focused coverage for factory-driven upload/download/exists round trips, missing-blob null/false behavior, overwrite semantics, case-insensitive identity, fail-fast validation, cloned-download immutability, and the `GetContainer(...)` namespace-establishing convention.
2. Narrow `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` to DI-focused coverage only, keeping backend selection, shared-state reuse, actionable missing-client diagnostics, and same-resource conflict behavior without burying the slice proof there.
3. Run the focused Blob suite, then the broader Blob filter, then the full solution sequentially with `./Azure.InMemory.sln` to preserve the infrastructure-free `dotnet test` loop.

## Must-Haves

- [ ] `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` directly proves the in-memory Blob upload/download/exists demo through `IAzureBlobFactory`
- [ ] Focused tests cover missing-blob `null`/`false` behavior, overwrite conflict vs replacement semantics, case-insensitive container/blob lookup, cloned content snapshots and preserved `contentType`, fail-fast validation for invalid names or `null` payloads, and the `GetContainer(...)` namespace-establishing convention
- [ ] `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` remains DI-focused and no longer carries the primary slice proof burden
- [ ] The Blob-focused, Blob-filtered, and full-solution test runs pass sequentially with `./Azure.InMemory.sln`

## Verification

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryBlobBehaviorTests`
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~Blob`
- `dotnet test ./Azure.InMemory.sln`

## Observability Impact

- Signals added/changed: named assertions over missing-blob, overwrite, case-insensitive, cloned snapshot, and container-bootstrap behavior plus continued use of `ContainerNames`, `ContainerExists(...)`, and `BlobExists(...)`
- How a future agent inspects this: run the focused Blob suite and inspect the assertions in `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs`
- Failure state exposed: whether the regression is in DI resolution, state validation, overwrite handling, missing-blob semantics, case-insensitive lookup, or content snapshot preservation

## Inputs

- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — stable Blob seam that the task must prove without widening
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs` — current in-memory adapter that the focused suite will exercise through DI
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — existing shared state root with container/blob inspection helpers and fail-fast validation
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — registration path that must continue to resolve the in-memory factory
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — current mixed registration-and-behavior proof to narrow back to DI coverage

## Expected Output

- `tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs` — focused behavior proof for the in-memory Blob seam
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — DI-focused registration suite with the slice proof extracted into the dedicated behavior file
