---
estimated_steps: 4
estimated_files: 4
skills_used:
  - error-handling-patterns
---

# T01: Create a standalone package-only consumer harness with local-feed restore guards

**Slice:** S03 — External consumer package proof
**Milestone:** M002

## Description

Establish the committed external-consumer boundary before adding behavior assertions. Create a consumer xUnit project under `samples/` that stays outside `Azure.InMemory.sln`, references `Azure.InMemory` only through `PackageReference`, opts out of repo-root Central Package Management, and carries a local `NuGet.Config` plus small repo-facing notes so future runs know how the proof stays package-only.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| Repo-root package settings in `Directory.Packages.props` | Stop on CPM inheritance failures such as `NU1008`, set `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` in the consumer project, and retry restore instead of working around the error manually. | Not applicable beyond the `dotnet restore` command itself. | Reject any consumer project shape that still relies on inherited root package versions or hidden repo state. |
| Local folder feed in `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` | Fail fast if restore cannot see `./artifacts/pack`; fix the relative path rather than falling back to a project reference or ad-hoc global source configuration. | Treat an unexpectedly slow restore as a feed/config problem and keep the task blocked until the local source is deterministic. | Reject a `NuGet.Config` that omits `nuget.org`, points at the wrong relative folder, or restores only because of unrelated global user config. |
| Producer-boundary guard in `Azure.InMemory.sln` and the consumer `.csproj` | Fail the task if the consumer project lands in `Azure.InMemory.sln` or grows a `ProjectReference`; that would invalidate the external-consumer proof. | Not applicable; these are static file checks. | Reject any harness that can compile without the packed library boundary. |

## Load Profile

- **Shared resources**: `./artifacts/pack` and `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages`
- **Per-operation cost**: one restore against a local folder feed plus the new consumer project evaluation
- **10x breakpoint**: stale or shared package cache state would mislead proof first, so the harness must keep a dedicated restore location and explicit `--no-cache` verification

## Negative Tests

- **Malformed inputs**: wrong relative feed path, missing `Azure.InMemory` version, or omitted `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`
- **Error paths**: restore should fail loudly instead of succeeding via hidden global NuGet sources or by sneaking in a `ProjectReference`
- **Boundary conditions**: the consumer project must stay outside `Azure.InMemory.sln` while still inheriting repo-root `net10.0` build defaults from `Directory.Build.props`

## Steps

1. Create `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` as a standalone xUnit test project that references `Azure.InMemory` via `PackageReference` and explicitly disables Central Package Management.
2. Add `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` with a local source pointing at `../../artifacts/pack` plus `nuget.org`, so restore is deterministic inside this repo.
3. Add `samples/Azure.InMemory.ExternalConsumer/README.md` with a short explanation of the package-only boundary, the dedicated restore path, and the fact that the project must stay out of `Azure.InMemory.sln`.
4. Run the consumer restore command with `--configfile`, `--packages`, and `--no-cache`, then assert that the project contains no `ProjectReference` and that `Azure.InMemory.sln` still excludes the sample.

## Must-Haves

- [ ] `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` contains `PackageReference Include="Azure.InMemory"` and no `ProjectReference`.
- [ ] The consumer `.csproj` sets `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` so repo-root CPM does not break restore.
- [ ] `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` resolves the local `./artifacts/pack` feed explicitly.
- [ ] The consumer harness restores successfully while remaining outside `Azure.InMemory.sln`.

## Verification

- `DOTNET_CLI_UI_LANGUAGE=en dotnet restore ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --configfile ./samples/Azure.InMemory.ExternalConsumer/NuGet.Config --packages ./samples/Azure.InMemory.ExternalConsumer/.nuget/packages --no-cache`
- `! rg -q "ProjectReference" ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj && ! rg -q "Azure.InMemory.ExternalConsumer" ./Azure.InMemory.sln && test -s ./samples/Azure.InMemory.ExternalConsumer/README.md`

## Observability Impact

- Signals added/changed: restore failures now surface CPM/feed misconfiguration directly at the consumer boundary.
- How a future agent inspects this: read `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj`, `samples/Azure.InMemory.ExternalConsumer/NuGet.Config`, and rerun the restore command.
- Failure state exposed: wrong feed path, hidden project coupling, and inherited CPM breakage become mechanically detectable instead of implicit.

## Inputs

- `Azure.InMemory.sln` — authoritative producer solution that the consumer harness must stay outside of.
- `Directory.Packages.props` — repo-root CPM setting that the consumer project must explicitly opt out of.
- `Directory.Build.props` — repo-root build defaults the consumer project will inherit.
- `README.md` — package-facing seam the consumer harness should follow.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — current local-feed artifact available for the initial restore proof.

## Expected Output

- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` — standalone package-only consumer test project with explicit CPM opt-out and package references.
- `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` — repo-local package source configuration pointing at `../../artifacts/pack` and `nuget.org`.
- `samples/Azure.InMemory.ExternalConsumer/README.md` — brief repo-facing instructions that explain the local-feed consumer boundary and restore command.
