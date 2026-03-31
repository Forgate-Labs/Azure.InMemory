---
id: T01
parent: S03
milestone: M002
provides: []
requires: []
affects: []
key_files: ["samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj", "samples/Azure.InMemory.ExternalConsumer/NuGet.Config", "samples/Azure.InMemory.ExternalConsumer/README.md", "samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Kept the sample outside `Azure.InMemory.sln`, disabled Central Package Management locally, and pinned all package versions in the sample `.csproj` so restore proof depends only on the packed artifact plus explicit package sources.", "Added the slice’s future consumer test file now as an analyzer-clean placeholder so the package-only project already compiles through the `.nupkg` boundary before T02 swaps in the real redelivery assertions."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Restored the consumer project using its sample-local `NuGet.Config`, an isolated `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages` cache, and `--no-cache`; that succeeded and produced a local `azure.inmemory/1.0.0` package folder plus an assets file whose `packageFolders` and `packagesPath` both point at the sample-local cache. Verified the sample contains no `ProjectReference`, remains excluded from `Azure.InMemory.sln`, and has the repo-facing README in place. Built the external consumer project with `--no-restore` to confirm the committed harness compiles through the packaged dependency boundary. For slice-level status, `dotnet test ./Azure.InMemory.sln` stayed green, while `bash ./scripts/verify-s03-external-consumer.sh` failed as expected because that script belongs to T03 and does not exist yet."
completed_at: 2026-03-31T12:38:33.996Z
blocker_discovered: false
---

# T01: Added a standalone package-only external consumer harness with deterministic local-feed restore guards.

> Added a standalone package-only external consumer harness with deterministic local-feed restore guards.

## What Happened
---
id: T01
parent: S03
milestone: M002
key_files:
  - samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj
  - samples/Azure.InMemory.ExternalConsumer/NuGet.Config
  - samples/Azure.InMemory.ExternalConsumer/README.md
  - samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Kept the sample outside `Azure.InMemory.sln`, disabled Central Package Management locally, and pinned all package versions in the sample `.csproj` so restore proof depends only on the packed artifact plus explicit package sources.
  - Added the slice’s future consumer test file now as an analyzer-clean placeholder so the package-only project already compiles through the `.nupkg` boundary before T02 swaps in the real redelivery assertions.
duration: ""
verification_result: mixed
completed_at: 2026-03-31T12:38:33.999Z
blocker_discovered: false
---

# T01: Added a standalone package-only external consumer harness with deterministic local-feed restore guards.

**Added a standalone package-only external consumer harness with deterministic local-feed restore guards.**

## What Happened

Created `samples/Azure.InMemory.ExternalConsumer/` as a committed consumer boundary outside `Azure.InMemory.sln`. The new `.csproj` inherits repo-root `net10.0` defaults from `Directory.Build.props`, opts out of repo-root Central Package Management with `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`, and references `Azure.InMemory` only through `PackageReference` with explicit xUnit package versions. Added a sample-local `NuGet.Config` that clears inherited package sources and points only at `../../artifacts/pack` and `nuget.org`, plus a README that documents the package-only guardrails and dedicated restore cache. Because this is the first task in a slice whose verification already names `ExternalConsumerQueueRedeliveryTests.cs`, I also created that file now as a temporary placeholder and fixed the inherited analyzer issues it surfaced so the sample compiles cleanly against the packaged dependency. Recorded the analyzer-clean placeholder-test gotcha in `.gsd/KNOWLEDGE.md` for future agents.

## Verification

Restored the consumer project using its sample-local `NuGet.Config`, an isolated `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages` cache, and `--no-cache`; that succeeded and produced a local `azure.inmemory/1.0.0` package folder plus an assets file whose `packageFolders` and `packagesPath` both point at the sample-local cache. Verified the sample contains no `ProjectReference`, remains excluded from `Azure.InMemory.sln`, and has the repo-facing README in place. Built the external consumer project with `--no-restore` to confirm the committed harness compiles through the packaged dependency boundary. For slice-level status, `dotnet test ./Azure.InMemory.sln` stayed green, while `bash ./scripts/verify-s03-external-consumer.sh` failed as expected because that script belongs to T03 and does not exist yet.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `DOTNET_CLI_UI_LANGUAGE=en dotnet restore ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --configfile ./samples/Azure.InMemory.ExternalConsumer/NuGet.Config --packages ./samples/Azure.InMemory.ExternalConsumer/.nuget/packages --no-cache` | 0 | ✅ pass | 16356ms |
| 2 | `! rg -q "ProjectReference" ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj && ! rg -q "Azure.InMemory.ExternalConsumer" ./Azure.InMemory.sln && test -s ./samples/Azure.InMemory.ExternalConsumer/README.md` | 0 | ✅ pass | 310ms |
| 3 | `DOTNET_CLI_UI_LANGUAGE=en dotnet build ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore` | 0 | ✅ pass | 3257ms |
| 4 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 5417ms |
| 5 | `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh` | 127 | ❌ fail | 306ms |


## Deviations

Created `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` one task earlier than the written T01 file list so the slice’s eventual package-only test file already exists and the sample project compiles cleanly before T02 replaces the placeholder with real assertions.

## Known Issues

`samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` is still a deliberate placeholder and will fail when executed until T02 adds the real queue redelivery proof. `scripts/verify-s03-external-consumer.sh` does not exist yet, so the slice-level script verification is expected to remain red until T03.

## Files Created/Modified

- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj`
- `samples/Azure.InMemory.ExternalConsumer/NuGet.Config`
- `samples/Azure.InMemory.ExternalConsumer/README.md`
- `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
Created `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` one task earlier than the written T01 file list so the slice’s eventual package-only test file already exists and the sample project compiles cleanly before T02 replaces the placeholder with real assertions.

## Known Issues
`samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` is still a deliberate placeholder and will fail when executed until T02 adds the real queue redelivery proof. `scripts/verify-s03-external-consumer.sh` does not exist yet, so the slice-level script verification is expected to remain red until T03.
