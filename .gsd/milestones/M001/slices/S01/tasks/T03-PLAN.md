---
estimated_steps: 5
estimated_files: 6
skills_used:
  - error-handling-patterns
---

# T03: Implement Blob SDK and in-memory provider registration

**Slice:** S01 — Provider registration and focused factories
**Milestone:** M001

## Description

Implement the Blob registration seam using the same explicit provider-selection pattern as Service Bus, but sized only for the basic S05 blob write/read flow. This task proves that Blob can switch between SDK and in-memory backends without affecting the shared factory contract and that the in-memory backend exposes a stable singleton state root for later slices.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| DI-registered `BlobServiceClient` | Throw an actionable `InvalidOperationException` that names the missing Blob SDK client dependency. | Not applicable at execution time; S01 registration must stay in-process and avoid network work. | Not applicable yet; Blob registration should not parse remote payloads in S01. |
| Resource registration mode selection | Throw an actionable `InvalidOperationException` when both SDK and in-memory Blob registrations are applied to the same service collection. | Not applicable; registration is synchronous. | Not applicable; invalid state is modeled as conflicting registrations. |

## Load Profile

- **Shared resources**: singleton `InMemoryBlobState` and DI container lifetime graph
- **Per-operation cost**: service registration plus service-provider construction; no storage I/O in S01 tests
- **10x breakpoint**: duplicated state-root registrations or inconsistent guard behavior would appear as wrong-type or wrong-instance assertions long before runtime load matters

## Negative Tests

- **Malformed inputs**: duplicate same-resource registration by calling both `AddAzureBlobSdk()` and `AddAzureBlobInMemory()`
- **Error paths**: SDK mode without the required `BlobServiceClient` registration should fail clearly
- **Boundary conditions**: repeated resolution of the in-memory backend should reuse the same `InMemoryBlobState`

## Steps

1. Refine `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` so it stays focused on the basic container/blob operations S05 will implement.
2. Implement `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs` as a thin adapter over a DI-registered `BlobServiceClient`.
3. Implement `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs` and `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs`, registering the state as a singleton root for later Blob behavior.
4. Add `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` with `AddAzureBlobSdk()` and `AddAzureBlobInMemory()` plus the same explicit conflict-guard pattern.
5. Add `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` covering SDK selection, in-memory selection, shared state reuse, and conflicting-registration failure behavior.

## Must-Haves

- [ ] `IAzureBlobFactory` stays focused on the S05 read/write seam.
- [ ] `AddAzureBlobSdk()` binds to a DI-registered `BlobServiceClient`.
- [ ] `AddAzureBlobInMemory()` exposes a singleton `InMemoryBlobState`.
- [ ] Conflicting same-resource registrations fail fast with actionable text.
- [ ] Targeted Blob registration tests pass.

## Verification

- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests`
- `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests.Conflicting`

## Observability Impact

- Signals added/changed: explicit conflict and missing-client exception messages plus assertions on resolved implementation types and singleton blob-state identity
- How a future agent inspects this: run `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests`
- Failure state exposed: which Blob registration path failed, whether the wrong backend resolved, and whether the blob state root stopped being shared

## Inputs

- `src/Azure.InMemory/Azure.InMemory.csproj` — library package references and assembly identity from T01
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — test framework and project reference from T01
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — base Blob seam to implement
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — prior art for the resource-specific guard pattern
- `.gsd/milestones/M001/slices/S01/S01-RESEARCH.md` — Blob seam scope and DI guidance

## Expected Output

- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — Blob registration extensions and guards
- `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs` — SDK-backed Blob adapter
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs` — in-memory Blob factory
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — singleton in-memory blob state root
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — focused Blob registration tests
