---
estimated_steps: 4
estimated_files: 3
skills_used:
  - error-handling-patterns
---

# T03: Automate the producer-to-package-to-consumer verification loop

**Slice:** S03 — External consumer package proof
**Milestone:** M002

## Description

Close the slice with a repeatable, cache-resistant verification path instead of one-off terminal history. Add a script that packs the current library into `./artifacts/pack`, clears or isolates the consumer package cache, restores the external consumer through its local `NuGet.Config`, runs the focused consumer test, and then reruns `dotnet test ./Azure.InMemory.sln` so the milestone keeps both producer and consumer proof green.

## Failure Modes

| Dependency | On error | On timeout | On malformed response |
|------------|----------|-----------|----------------------|
| `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` | Stop immediately if pack fails; the consumer proof is invalid without a fresh package artifact. | Treat an unexpectedly slow pack as a local toolchain blocker and leave the script failing rather than swallowing the issue. | Reject a script that uses a stale artifact or packs from the wrong project path. |
| Consumer restore/test commands | Fail fast on restore or test errors and preserve the failing stage in script output so future agents know whether the boundary broke at feed config, restore, compile, or runtime assertions. | Treat hangs as verification failures; do not add retry loops that hide deterministic proof problems. | Reject a script that silently falls back to cached packages, missing `NuGet.Config`, or `dotnet test` with implicit restore. |
| `dotnet test ./Azure.InMemory.sln` producer regression guard | Stop on any producer regression after the consumer proof passes; the milestone still needs the main solution green. | Treat hangs as blockers instead of partial success. | Reject a script that proves only the consumer path while leaving the producer solution unchecked. |

## Load Profile

- **Shared resources**: `./artifacts/pack`, `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages`, and the repo-wide .NET build graph
- **Per-operation cost**: one Release pack, one external-consumer restore, one focused consumer test run, and one full producer-solution test run
- **10x breakpoint**: stale package cache or pack output drift would mislead proof first, so the script must isolate restore state and fail on the first broken stage

## Negative Tests

- **Malformed inputs**: missing script shebang, wrong relative paths, omitted `--configfile`, or missing `--no-restore` on the focused consumer test
- **Error paths**: restore/test/solution failures must stop the script at the failing phase with actionable command output
- **Boundary conditions**: rerunning the script should still consume the freshly packed artifact instead of succeeding only because a previous consumer cache remained warm

## Steps

1. Create `scripts/verify-s03-external-consumer.sh` as a fail-fast shell script that packs the producer project into `./artifacts/pack` and recreates or isolates the consumer package cache.
2. Make the script restore `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` with its local `NuGet.Config`, `--packages`, and `--no-cache`, then run the focused consumer test with `--no-restore`.
3. Finish the script by running `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln` so the external proof and the producer regression loop stay tied together.
4. Update `samples/Azure.InMemory.ExternalConsumer/README.md` so the single verification command is discoverable and its expected stages are documented.

## Must-Haves

- [ ] `scripts/verify-s03-external-consumer.sh` is the single command entrypoint for pack → restore → consumer test → solution test.
- [ ] The script uses the consumer project's `NuGet.Config`, a dedicated packages directory, and `--no-cache`/`--no-restore` guards so proof does not rely on stale state.
- [ ] The script fails fast and leaves the failing verification stage obvious in terminal output.
- [ ] The sample README points future agents at the script instead of requiring manual command reconstruction.

## Verification

- `bash -n ./scripts/verify-s03-external-consumer.sh`
- `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh`

## Observability Impact

- Signals added/changed: the end-to-end proof now has one deterministic command whose stage output shows whether failure happened during pack, restore, focused consumer test, or producer regression.
- How a future agent inspects this: read and run `scripts/verify-s03-external-consumer.sh`, then inspect the sample README for the documented verification flow.
- Failure state exposed: stale artifacts, feed errors, consumer assertion regressions, and producer regressions become localized to one script stage instead of scattered terminal history.

## Inputs

- `src/Azure.InMemory/Azure.InMemory.csproj` — producer package source to pack freshly for the proof loop.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — existing package artifact pattern that the script refreshes.
- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` — committed consumer project to restore and test.
- `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` — local feed configuration for restore.
- `Azure.InMemory.sln` — authoritative producer regression loop that must stay green.

## Expected Output

- `scripts/verify-s03-external-consumer.sh` — single command entrypoint for the full producer-to-package-to-consumer verification loop.
- `samples/Azure.InMemory.ExternalConsumer/README.md` — updated consumer-proof instructions that point to the verification script and restore/test expectations.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — freshly packed artifact exercised by the verification loop.
