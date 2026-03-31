# S02: Internal-ready package surface and docs

**Goal:** Make Azure.InMemory feel intentional and consumable as an internal package by wiring real NuGet metadata, a package-safe quickstart, and objective pack proof around the existing deterministic Service Bus seam.
**Demo:** After this: `dotnet pack` emits an internal-ready Azure.InMemory package, and package-facing docs/examples show another team how to install the package and wire the in-memory Service Bus provider without guessing hidden setup.

## Tasks
- [x] **T01: Added intentional Azure.InMemory package metadata, packed the root README into the nupkg, and proved the 1.0.0 package artifact and solution regression locally.** — ## Description

Make the producer project pack like a deliberate internal package instead of a default class library. Wire the root `README.md` into the package, add the minimum metadata an internal consumer expects, and choose an explicit baseline version strategy that keeps S03 free to override package versioning during local-consumer proof.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `dotnet pack` metadata generation for `src/Azure.InMemory/Azure.InMemory.csproj` | Stop on the first pack error or warning caused by invalid metadata/readme wiring and fix the project file rather than shipping a partially-described package. | Treat any unexpected hang as a packaging regression; this task should complete in one normal local `dotnet pack` run. | Reject broken metadata such as an unreadable readme path or invalid license/version settings before finishing the task. |
| Root-package readme linkage from `src/Azure.InMemory/Azure.InMemory.csproj` to `README.md` | Fail the task if the package still emits the missing-readme warning or omits `README.md` from the `.nupkg`. | Not applicable beyond the pack command itself. | Keep the package-safe file path and metadata deterministic so later consumers do not depend on repo-only relative links or accidental defaults. |

## Load Profile

- **Shared resources**: MSBuild pack targets plus the shared `./artifacts/pack` output directory
- **Per-operation cost**: one Release pack evaluation and one `.nupkg` write for the library project
- **10x breakpoint**: stale output/version collisions would mislead later consumer proof first, so the project file must declare an intentional baseline version instead of relying on defaults

## Negative Tests

- **Malformed inputs**: invalid or missing readme path, blank package metadata values, or non-positive version overrides should fail through pack-time diagnostics
- **Error paths**: `dotnet pack` must stop warning about a missing package readme once the project file is fixed
- **Boundary conditions**: the library should still pack from `./src/Azure.InMemory/Azure.InMemory.csproj` using the repo-root `README.md` and existing root `LICENSE`

## Steps

1. Update `src/Azure.InMemory/Azure.InMemory.csproj` with intentional internal-package metadata: explicit package identity/version baseline, authors/company, tags, project/repository URL, repository type, package readme, and MIT license expression.
2. Pack the authoritative root `README.md` into the package from the project file instead of creating a packaging-only duplicate.
3. Keep the versioning strategy explicit and compatible with later local-feed proof so S03 can override package versioning without relying on NuGet cache luck.
4. Run `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` once to confirm the project metadata is valid before handoff.

## Must-Haves

- [ ] `src/Azure.InMemory/Azure.InMemory.csproj` declares intentional package metadata instead of relying on default sparse nuspec values.
- [ ] The package readme is the authoritative root `README.md`, packed under the expected `README.md` package path.
- [ ] The project keeps an explicit baseline package version that later local-consumer proof can override intentionally.
- [ ] A local `dotnet pack` run no longer warns that the package is missing a readme.
  - Estimate: 75m
  - Files: src/Azure.InMemory/Azure.InMemory.csproj, README.md, LICENSE
  - Verify: dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack
- [x] **T02: Replaced the placeholder package readme with a real Service Bus quickstart that teaches DI registration, explicit topology, deterministic reruns, and test-only inspection truthfully.** — ## Description

Turn the placeholder root `README.md` into the single package-facing guide an internal consumer can follow without guessing hidden setup. Keep it truthful to the focused factory seam, the explicit topology requirements, and S01's deterministic rerun semantics so S03 can later use the same guide for a fresh consumer proof.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Public Service Bus registration and factory seam in `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` and `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` | If the docs cannot point to a real public API, stop and reconcile the README with the existing seam instead of inventing package-only abstractions. | Not applicable; documentation work is local file editing. | Do not publish incorrect imports or call patterns such as raw Azure SDK clients when the seam requires `IAzureServiceBusFactory`. |
| Redelivery/inspection behavior proved in `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`, `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`, and the Service Bus tests | If docs drift from the tested behavior, correct the README rather than softening the wording. | Not applicable; behavior truth comes from the existing code and tests. | Never imply background retries, topic-local pending queues, or subscription paths that differ from canonical `<topic>/Subscriptions/<subscription>`. |

## Load Profile

- **Shared resources**: the single-sourced `README.md` package readme and the existing public Service Bus seam it documents
- **Per-operation cost**: one authoritative doc update that replaces the placeholder root guide
- **10x breakpoint**: documentation drift across multiple guides would break first, so this task keeps one authoritative package-facing quickstart instead of splitting examples across new docs files

## Negative Tests

- **Malformed inputs**: wrong namespaces, missing `PackageReference`/install instructions, or examples that skip topology declaration must be treated as documentation bugs
- **Error paths**: README examples must explain that a successful handler without `CompleteMessageAsync(...)` leaves the message pending unless `AutoCompleteMessages: true` is chosen
- **Boundary conditions**: docs must cover both queue setup and canonical subscription inspection while keeping `InMemoryServiceBusState` framed as test-only observability

## Steps

1. Replace the placeholder root `README.md` with an internal-consumer overview, installation snippet, and a short explanation of the explicit Azure resource factory model.
2. Add a concrete Service Bus quickstart that shows `using Azure.InMemory.DependencyInjection;`, resolving `IAzureServiceBusFactory`, declaring topology, and calling `AddAzureServiceBusInMemory()` from DI.
3. Document processor settlement and retry truthfully: explain explicit `CompleteMessageAsync(...)`, the optional `AutoCompleteMessages: true` path, and that failed deliveries reappear only on the next explicit `StartProcessingAsync()` run.
4. Document the test-only `InMemoryServiceBusState` inspection surface, including canonical subscription entity paths in the form `<topic>/Subscriptions/<subscription>`.
5. Keep the README package-safe by avoiding repo-only assumptions and by framing external-consumer/local-feed proof as the next-slice concern rather than pretending S02 already delivered it.

## Must-Haves

- [ ] `README.md` explains installation and DI registration for the packaged library.
- [ ] The quickstart uses the real public namespaces and `IAzureServiceBusFactory` seam rather than raw Azure SDK clients.
- [ ] The docs explicitly preserve S01's deterministic redelivery semantics and canonical subscription-path rule.
- [ ] `InMemoryServiceBusState` is documented as a test-only inspection surface, not the primary application seam.
  - Estimate: 90m
  - Files: README.md, src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs, src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs, src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs, tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs
  - Verify: test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md
- [x] **T03: Re-ran the solution regression, emitted a fresh Azure.InMemory package, and recorded durable inspection proof that the nupkg ships the intended README and NuGet metadata surface.** — ## Description

Close the slice with objective evidence instead of trusting source edits alone. Re-run the authoritative solution tests, emit a fresh package artifact, and inspect the `.nupkg` contents so S02 finishes with proof that the package includes the intended metadata/readme surface another team would actually see.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `dotnet test ./Azure.InMemory.sln` regression guard | Stop on any failing test and fix the packaging/docs changes instead of letting S02 silently regress validated seams. | Treat an unexpected hang as a local toolchain problem that blocks slice completion. | Not applicable; the solution test run already encodes the typed regression surface. |
| `dotnet pack` plus `.nupkg`/`.nuspec` inspection | Fail the task if the pack output or artifact inspection shows missing readme inclusion, missing metadata, or stale output. | Treat a stuck pack or inspection command as a blocker; do not declare the slice done without the artifact. | Reject a package that builds but omits the expected `README.md`, MIT license expression, or repository URL metadata. |

## Load Profile

- **Shared resources**: `./artifacts/pack`, local NuGet-style package output, and the solution-wide build/test graph
- **Per-operation cost**: one full solution test run, one Release pack, and one zip/nuspec inspection pass
- **10x breakpoint**: stale package files or same-version cache confusion would mislead downstream consumer proof first, so this task must inspect the newly emitted artifact explicitly

## Negative Tests

- **Malformed inputs**: a package without `README.md`, without the intended nuspec metadata, or with broken docs references is a failure even if `dotnet pack` itself exits successfully
- **Error paths**: if tests or pack fail after the docs/metadata changes, fix `README.md` and `src/Azure.InMemory/Azure.InMemory.csproj` within this slice instead of deferring broken proof downstream
- **Boundary conditions**: the emitted package should include the authoritative readme and preserve the solution's in-process test loop with no external infrastructure

## Steps

1. Run `dotnet test ./Azure.InMemory.sln` from the active M002 root to preserve the validated seam before declaring the package ready.
2. Emit a fresh package with `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`, replacing any stale artifact in that output folder if needed.
3. Inspect the emitted `artifacts/pack/Azure.InMemory.1.0.0.nupkg` and generated nuspec metadata, and write a concise inspection summary to `artifacts/pack/package-inspection.txt`.
4. Do not expand into the full fresh-consumer proof from S03; this task ends once the package artifact, readme inclusion, and metadata/docs evidence are all concrete.

## Must-Haves

- [ ] `dotnet test ./Azure.InMemory.sln` passes after the package-surface edits.
- [ ] `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` emits `artifacts/pack/Azure.InMemory.1.0.0.nupkg`.
- [ ] `artifacts/pack/package-inspection.txt` records that the package includes `README.md` and the intended nuspec metadata.
- [ ] The task stops short of creating a fresh consumer project; that acceptance remains owned by S03.
  - Estimate: 60m
  - Files: src/Azure.InMemory/Azure.InMemory.csproj, README.md, Azure.InMemory.sln, tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj
  - Verify: dotnet test ./Azure.InMemory.sln && dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack && test -f ./artifacts/pack/Azure.InMemory.1.0.0.nupkg && test -s ./artifacts/pack/package-inspection.txt
