---
estimated_steps: 4
estimated_files: 3
skills_used: []
---

# T01: Harden the library project into an intentional internal package

**Slice:** S02 — Internal-ready package surface and docs
**Milestone:** M002

## Description

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

## Verification

- `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`

## Observability Impact

- Signals added/changed: pack-time metadata/readme warnings become meaningful completion signals for the library package surface.
- How a future agent inspects this: inspect `./artifacts/pack/Azure.InMemory.1.0.0.nupkg` and the generated nuspec after running the pack command.
- Failure state exposed: missing readme inclusion, invalid metadata, or same-version output confusion are visible directly in pack output and package contents.

## Inputs

- `src/Azure.InMemory/Azure.InMemory.csproj` — current sparse package metadata surface to harden.
- `README.md` — authoritative root document that should become the packed readme.
- `LICENSE` — existing MIT license text to reference via package metadata.

## Expected Output

- `src/Azure.InMemory/Azure.InMemory.csproj` — internal-ready package metadata and readme inclusion.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — packed library artifact proving the metadata/readme wiring works.
