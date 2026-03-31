---
estimated_steps: 4
estimated_files: 5
skills_used: []
---

# T03: Prove the packed artifact and docs are internally consumable from the producer boundary

**Slice:** S02 — Internal-ready package surface and docs
**Milestone:** M002

## Description

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

## Verification

- `dotnet test ./Azure.InMemory.sln`
- `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`
- `test -f ./artifacts/pack/Azure.InMemory.1.0.0.nupkg && test -s ./artifacts/pack/package-inspection.txt`

## Observability Impact

- Signals added/changed: the emitted package and inspection text become durable package-health evidence instead of relying on transient terminal output.
- How a future agent inspects this: read `artifacts/pack/package-inspection.txt` and, if needed, unzip `artifacts/pack/Azure.InMemory.1.0.0.nupkg` to inspect the nuspec/readme directly.
- Failure state exposed: missing readme inclusion, missing metadata, or stale/broken pack output are visible without rerunning exploratory commands.

## Inputs

- `Azure.InMemory.sln` — authoritative solution health check for validated seams.
- `src/Azure.InMemory/Azure.InMemory.csproj` — package metadata and readme wiring source of truth.
- `README.md` — packaged quickstart content to verify inside the artifact.
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — existing test project confirming the in-process loop still builds and runs.

## Expected Output

- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — fresh package artifact emitted from the producer boundary.
- `artifacts/pack/package-inspection.txt` — durable inspection evidence for packaged readme and nuspec metadata.
