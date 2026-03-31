# S03: External consumer package proof — UAT

**Milestone:** M002
**Written:** 2026-03-31T12:55:18.111Z

# S03 UAT — External consumer package proof

## Preconditions
- Run from the repository root in this worktree: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M002`.
- .NET SDK for `net10.0` is installed.
- `bash` and `python3` are available.
- No manual package-source setup is required; the sample's `NuGet.Config` must provide the restore sources.

## Test Case 1 — End-to-end producer-to-package-to-consumer verification
1. Run `DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh`.
   - Expected: the script prints the stages `Pack Azure.InMemory into ./artifacts/pack`, `Recreate isolated consumer package cache`, `Restore external consumer with sample-local NuGet.Config and no cache`, `Run focused external consumer package proof without restore`, `Run producer solution regression suite`, and `Verification loop completed successfully`.
2. Observe the consumer-proof stage output.
   - Expected: the sample project builds from `PackageReference` and the focused suite passes with `Passed: 3, Failed: 0`.
3. Observe the producer regression stage output.
   - Expected: `dotnet test ./Azure.InMemory.sln` passes with `Passed: 74, Failed: 0`.
4. Verify the final line.
   - Expected: `Fresh package verified through consumer and producer test boundaries.`

## Test Case 2 — Package-only restore boundary guardrails
1. Run `DOTNET_CLI_UI_LANGUAGE=en dotnet restore ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --configfile ./samples/Azure.InMemory.ExternalConsumer/NuGet.Config --packages ./samples/Azure.InMemory.ExternalConsumer/.nuget/packages --no-cache`.
   - Expected: restore succeeds without needing repo-level or machine-level package sources.
2. Run `rg -n "ProjectReference" ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj`.
   - Expected: no matches.
3. Run `rg -n "Azure.InMemory.ExternalConsumer" ./Azure.InMemory.sln`.
   - Expected: no matches.
4. Inspect `./samples/Azure.InMemory.ExternalConsumer/NuGet.Config`.
   - Expected: it clears inherited sources and includes only `../../artifacts/pack` plus `https://api.nuget.org/v3/index.json`.

## Test Case 3 — External consumer behavior proof details
1. Run `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore --filter FullyQualifiedName~ExternalConsumerQueueRedeliveryTests`.
   - Expected: the focused sample suite passes with exactly 3 tests.
2. Review the test names in the runner output or source file `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs`.
   - Expected: the suite covers:
     - an intentional first-run queue failure that leaves one pending message with `DeliveryCount == 2`, records one errored outcome, and then completes on a second explicit `StartProcessingAsync()` run;
     - undeclared queue processing that throws an actionable `InvalidOperationException` mentioning `CreateQueueAsync`;
     - wrong-queue processing that leaves the intended queue message pending instead of being consumed by another processor.
3. If Test Case 3 fails, inspect `InMemoryServiceBusState` assertions in the sample test file.
   - Expected: failure details point to queue pending/completed/dead-lettered/errored state or queue-topology guidance rather than hidden repo-only helpers.

## Test Case 4 — Packaged artifact inspection
1. Run the verifier once so `artifacts/pack/Azure.InMemory.1.0.0.nupkg` is freshly emitted.
   - Expected: the package file exists under `./artifacts/pack`.
2. Inspect the package with `python3`/zip tooling.
   - Expected: the package contains `README.md`, `lib/net10.0/Azure.InMemory.dll`, and a nuspec that reports `Azure.InMemory` id, version `1.0.0`, Forgate Labs authorship, MIT license expression, and repository/project URLs.
3. Open the packaged `README.md` content.
   - Expected: it still documents `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, deterministic `StartProcessingAsync()` reruns, `InMemoryServiceBusState`, and canonical `<topic>/Subscriptions/<subscription>` inspection guidance.

## Edge Cases To Watch
- If restore succeeds only after using a global/shared NuGet cache, the proof is invalid; rerun after deleting `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages` and confirm the sample-local restore still passes.
- If the consumer project ever appears in `Azure.InMemory.sln` or gains a `ProjectReference`, the slice no longer proves real package consumption.
- If the first consumer test starts expecting `Assert.ThrowsAsync` from `StartProcessingAsync()`, the proof is drifting away from the actual redelivery contract; handler failures must be observed through `ProcessErrorAsync` and `InMemoryServiceBusState`.
