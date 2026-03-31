---
id: T03
parent: S03
milestone: M002
provides: []
requires: []
affects: []
key_files: ["scripts/verify-s03-external-consumer.sh", "samples/Azure.InMemory.ExternalConsumer/README.md", "artifacts/pack/Azure.InMemory.1.0.0.nupkg"]
key_decisions: ["Resolved all inputs from the script location and recreated the consumer-local `.nuget/packages` folder on every run so the proof does not depend on caller cwd or stale same-version package cache state.", "Kept the verifier stage-oriented with explicit `==>` output and a failing-stage trap so pack, restore, focused consumer test, and producer regression failures stay localized in one command."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "`bash -n ./scripts/verify-s03-external-consumer.sh` passed. `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh` passed end to end, producing a fresh `Azure.InMemory.1.0.0.nupkg`, passing all 3 external-consumer tests, and passing all 74 producer-solution tests. A targeted `rg` check confirmed the script and README expose the expected stage output and cache-isolation guards (`--configfile`, `--packages`, `--no-cache`, `--no-restore`)."
completed_at: 2026-03-31T12:50:01.081Z
blocker_discovered: false
---

# T03: Added a single-command verifier that repacks Azure.InMemory, restores the external consumer from the sample-local feed, reruns the package-only redelivery proof, and then checks the producer solution.

> Added a single-command verifier that repacks Azure.InMemory, restores the external consumer from the sample-local feed, reruns the package-only redelivery proof, and then checks the producer solution.

## What Happened
---
id: T03
parent: S03
milestone: M002
key_files:
  - scripts/verify-s03-external-consumer.sh
  - samples/Azure.InMemory.ExternalConsumer/README.md
  - artifacts/pack/Azure.InMemory.1.0.0.nupkg
key_decisions:
  - Resolved all inputs from the script location and recreated the consumer-local `.nuget/packages` folder on every run so the proof does not depend on caller cwd or stale same-version package cache state.
  - Kept the verifier stage-oriented with explicit `==>` output and a failing-stage trap so pack, restore, focused consumer test, and producer regression failures stay localized in one command.
duration: ""
verification_result: passed
completed_at: 2026-03-31T12:50:01.083Z
blocker_discovered: false
---

# T03: Added a single-command verifier that repacks Azure.InMemory, restores the external consumer from the sample-local feed, reruns the package-only redelivery proof, and then checks the producer solution.

**Added a single-command verifier that repacks Azure.InMemory, restores the external consumer from the sample-local feed, reruns the package-only redelivery proof, and then checks the producer solution.**

## What Happened

Created `scripts/verify-s03-external-consumer.sh` as the slice’s authoritative verification entrypoint. The script is a fail-fast Bash flow with `set -euo pipefail`, repo-root path resolution from the script location, stage banners, and an exit trap that reports the failing stage when a command breaks. It refreshes `artifacts/pack/Azure.InMemory.1.0.0.nupkg` from `src/Azure.InMemory/Azure.InMemory.csproj`, recreates `samples/Azure.InMemory.ExternalConsumer/.nuget/packages`, restores the external consumer through its sample-local `NuGet.Config` with `--packages` and `--no-cache`, runs the focused `ExternalConsumerQueueRedeliveryTests` proof with `--no-restore`, and finishes by rerunning `dotnet test ./Azure.InMemory.sln` as the producer regression guard. Updated `samples/Azure.InMemory.ExternalConsumer/README.md` so the single verification command and expected stages are discoverable without reconstructing the flow from terminal history.

## Verification

`bash -n ./scripts/verify-s03-external-consumer.sh` passed. `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh` passed end to end, producing a fresh `Azure.InMemory.1.0.0.nupkg`, passing all 3 external-consumer tests, and passing all 74 producer-solution tests. A targeted `rg` check confirmed the script and README expose the expected stage output and cache-isolation guards (`--configfile`, `--packages`, `--no-cache`, `--no-restore`).

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `bash -n ./scripts/verify-s03-external-consumer.sh` | 0 | ✅ pass | 4ms |
| 2 | `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh` | 0 | ✅ pass | 29003ms |
| 3 | `rg -n --fixed-strings '==> ' ./scripts/verify-s03-external-consumer.sh && rg -n --fixed-strings -- '--configfile' ./scripts/verify-s03-external-consumer.sh && rg -n --fixed-strings -- '--packages' ./scripts/verify-s03-external-consumer.sh && rg -n --fixed-strings -- '--no-cache' ./scripts/verify-s03-external-consumer.sh && rg -n --fixed-strings -- '--no-restore' ./scripts/verify-s03-external-consumer.sh && rg -n 'Single-command verification|verify-s03-external-consumer.sh|fails fast|producer regression guard' ./samples/Azure.InMemory.ExternalConsumer/README.md` | 0 | ✅ pass | 45ms |


## Deviations

Created `artifacts/pack` inside the script when missing instead of assuming the directory already exists, so the verifier remains usable from a fresh worktree while still failing if required inputs are absent.

## Known Issues

None.

## Files Created/Modified

- `scripts/verify-s03-external-consumer.sh`
- `samples/Azure.InMemory.ExternalConsumer/README.md`
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg`


## Deviations
Created `artifacts/pack` inside the script when missing instead of assuming the directory already exists, so the verifier remains usable from a fresh worktree while still failing if required inputs are absent.

## Known Issues
None.
