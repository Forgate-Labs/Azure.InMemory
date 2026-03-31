---
id: T03
parent: S01
milestone: M001
provides: []
requires: []
affects: []
key_files: ["src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs", "src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs", "src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs", "src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs", "tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Resolved the SDK-backed Blob factory through a DI activation lambda so missing BlobServiceClient dependencies fail when IAzureBlobFactory is resolved, with actionable AddAzureBlobSdk() guidance.", "Made the in-memory Blob backend truthful for the S05 seam by storing cloned BinaryData plus content type in a singleton InMemoryBlobState instead of leaving registration-only stubs."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests and dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests.Conflicting. Both passed, proving SDK vs in-memory resolution, singleton InMemoryBlobState reuse, actionable missing BlobServiceClient failures, and fail-fast conflicting Blob registration behavior. The targeted suite also exercised a basic in-memory upload/download/exists flow against the shared state root."
completed_at: 2026-03-30T20:56:02.840Z
blocker_discovered: false
---

# T03: Added Blob SDK/in-memory registrations with shared in-memory blob state and focused provider tests.

> Added Blob SDK/in-memory registrations with shared in-memory blob state and focused provider tests.

## What Happened
---
id: T03
parent: S01
milestone: M001
key_files:
  - src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs
  - src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs
  - src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs
  - src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Resolved the SDK-backed Blob factory through a DI activation lambda so missing BlobServiceClient dependencies fail when IAzureBlobFactory is resolved, with actionable AddAzureBlobSdk() guidance.
  - Made the in-memory Blob backend truthful for the S05 seam by storing cloned BinaryData plus content type in a singleton InMemoryBlobState instead of leaving registration-only stubs.
duration: ""
verification_result: passed
completed_at: 2026-03-30T20:56:02.842Z
blocker_discovered: false
---

# T03: Added Blob SDK/in-memory registrations with shared in-memory blob state and focused provider tests.

**Added Blob SDK/in-memory registrations with shared in-memory blob state and focused provider tests.**

## What Happened

Implemented the Blob provider seam end-to-end while keeping the existing IAzureBlobFactory contract intact because it already matched the planned S05 read/write surface. Added AzureBlobSdkFactory as a thin adapter over a DI-registered BlobServiceClient with upload, download, and existence support plus actionable resolution failures when AddAzureBlobSdk() is used without the required client. Added InMemoryBlobFactory and a singleton InMemoryBlobState that truthfully stores cloned BinaryData payloads and content types under container/blob names so the in-memory backend already supports the basic upload/download/exists flow. Added AddAzureBlobSdk() and AddAzureBlobInMemory() with explicit same-resource conflict guards, replaced the placeholder Blob registration test suite with focused backend-selection/shared-state/failure-path coverage, and documented the non-obvious Blob testability and container-namespace behavior in .gsd/KNOWLEDGE.md.

## Verification

Ran dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests and dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests.Conflicting. Both passed, proving SDK vs in-memory resolution, singleton InMemoryBlobState reuse, actionable missing BlobServiceClient failures, and fail-fast conflicting Blob registration behavior. The targeted suite also exercised a basic in-memory upload/download/exists flow against the shared state root.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests` | 0 | ✅ pass | 3513ms |
| 2 | `dotnet test Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests.Conflicting` | 0 | ✅ pass | 3461ms |


## Deviations

None. The existing Blob factory contract already matched the planned S05 seam, so no public contract change was necessary.

## Known Issues

Key Vault provider registration and mixed-resource composition tests remain pending for T04 and T05, so the full slice verification surface is not complete yet.

## Files Created/Modified

- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs`
- `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs`
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobFactory.cs`
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
None. The existing Blob factory contract already matched the planned S05 seam, so no public contract change was necessary.

## Known Issues
Key Vault provider registration and mixed-resource composition tests remain pending for T04 and T05, so the full slice verification surface is not complete yet.
