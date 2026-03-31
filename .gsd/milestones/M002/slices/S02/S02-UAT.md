# S02: Internal-ready package surface and docs — UAT

**Milestone:** M002
**Written:** 2026-03-31T03:30:57.644Z

# S02: Internal-ready package surface and docs — UAT

**Milestone:** M002  
**Written:** 2026-03-30T22:56:19-03:00

## Preconditions
- Working directory: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M002`
- .NET 10 SDK is installed.
- The active root contains `./Azure.InMemory.sln`, `./src/Azure.InMemory/...`, `./README.md`, and `./artifacts/pack/...`.
- No Azure resources, Docker containers, or external emulators are required.
- Run the verification commands from this worktree using the explicit relative solution path `./Azure.InMemory.sln`.

## Test Case 1 — The producer project packs into an intentional internal package
1. Run `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`.
   - Expected: the command succeeds and emits `./artifacts/pack/Azure.InMemory.1.0.0.nupkg`.
2. Inspect `src/Azure.InMemory/Azure.InMemory.csproj`.
   - Expected: it declares explicit package metadata including `PackageId` `Azure.InMemory`, `Version` `1.0.0`, authors/company, project/repository URLs, `RepositoryType`, `PackageReadmeFile`, and `PackageLicenseExpression`.
3. Confirm the readme packing entry in the project file.
   - Expected: the root `README.md` is packed via the project file (`..\..\README.md`) and linked as `README.md` inside the package instead of maintaining a duplicate packaging-only readme.

## Test Case 2 — The package-facing README teaches the real Service Bus seam and setup
1. Run `test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md`.
   - Expected: the command succeeds and shows the required package install, DI registration, seam, settlement, rerun, inspection, and canonical subscription-path markers.
2. Review the README quickstart.
   - Expected: it shows `using Azure.InMemory.DependencyInjection;`, `services.AddAzureServiceBusInMemory();`, resolution of `IAzureServiceBusFactory`, explicit queue/topic/subscription creation through `factory.Administration`, sender creation, and processor creation through the factory seam.
3. Confirm the settlement and retry guidance.
   - Expected: the README states that successful handlers must call `CompleteMessageAsync(...)` unless `AutoCompleteMessages: true` is chosen, and that failed deliveries reappear only on the next explicit `StartProcessingAsync()` run.
4. Edge check.
   - Expected: the README frames `InMemoryServiceBusState` as test-only observability and uses the literal canonical subscription entity path `<topic>/Subscriptions/<subscription>` instead of implying repo-internal helpers or topic-name inspection.

## Test Case 3 — The emitted `.nupkg` actually contains the intended readme and nuspec metadata
1. Run a direct package inspection against `./artifacts/pack/Azure.InMemory.1.0.0.nupkg` (for example, a `python3` zip inspection).
   - Expected: the package contains `README.md` and an embedded `.nuspec` file.
2. Inspect the embedded nuspec metadata.
   - Expected: it includes `id` `Azure.InMemory`, version `1.0.0`, authors `Forgate Labs`, `<readme>README.md</readme>`, MIT license expression, project URL `https://github.com/Forgate-Labs/Azure.InMemory`, and repository `https://github.com/Forgate-Labs/Azure.InMemory.git` with type `git`.
3. Inspect the packaged `README.md` inside the `.nupkg`.
   - Expected: it still contains the package-facing quickstart markers `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, `StartProcessingAsync()`, `InMemoryServiceBusState`, and `<topic>/Subscriptions/<subscription>`.
4. Review `./artifacts/pack/package-inspection.txt`.
   - Expected: it records the same metadata and README-marker proof durably for downstream verification.

## Test Case 4 — Packaging/docs changes do not regress the infrastructure-free solution loop
1. Run `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln`.
   - Expected: the solution passes from the active M002 root with no Azure, Docker, or external infrastructure.
2. Confirm the overall result.
   - Expected: package metadata/readme changes do not break the existing Service Bus, Blob, or Key Vault seams, and the internal package surface is proven from the producer boundary.

## Final Slice Proof
1. Run the full slice verification set in order:
   - `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln`
   - `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`
   - `test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md`
   - a direct `python3` inspection of `./artifacts/pack/Azure.InMemory.1.0.0.nupkg`
   - `test -f ./artifacts/pack/Azure.InMemory.1.0.0.nupkg && test -s ./artifacts/pack/package-inspection.txt`
   - Expected: every command passes.
2. Confirm the slice outcome.
   - Expected: `dotnet pack` emits an internal-ready `Azure.InMemory` package, package-facing docs show another team how to install and wire `AddAzureServiceBusInMemory()` without guessing hidden setup, and producer-boundary inspection proves the `.nupkg` actually ships that readme and metadata surface.

## Failure Signals
- `dotnet pack` fails or does not emit `./artifacts/pack/Azure.InMemory.1.0.0.nupkg`.
- The README is missing package install, `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, settlement/rerun guidance, `InMemoryServiceBusState`, or canonical subscription-path documentation.
- Direct `.nupkg` inspection shows missing `README.md`, missing MIT license/readme/repository metadata, or packaged README content that drifted from the root guide.
- `dotnet test ./Azure.InMemory.sln` fails, indicating the package-surface work regressed the infrastructure-free test loop.

## Not Proven By This UAT
- A fresh external consumer restoring `Azure.InMemory` from a local NuGet source and executing its own test project. That remains the acceptance target for S03.
- Public NuGet publication, broader marketplace metadata polish, or any new Service Bus runtime semantics beyond the already-documented deterministic in-memory seam.

## Notes for Tester
- In this WSL worktree, use `python3` rather than `python` for zip-based package inspection.
- If the same package version is repacked locally, inspect the emitted `.nupkg` directly instead of trusting `dotnet pack` alone; direct zip/nuspec inspection is the reliable way to catch stale same-version artifacts or missing packaged docs.
