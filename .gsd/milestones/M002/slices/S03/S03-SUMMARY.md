---
id: S03
parent: M002
milestone: M002
provides:
  - A committed external xUnit consumer project that restores `Azure.InMemory` from a local NuGet feed without producer solution membership or project references.
  - Package-only queue redelivery tests that prove explicit retry-state inspection and second-run completion through the documented Service Bus seam.
  - A single-command verification script that replays the whole pack → restore → consumer test → producer regression loop deterministically.
requires:
  - slice: S01
    provides: Deterministic Service Bus redelivery behavior, delivery-count progression, and inspectable errored/pending/completed outcomes through `InMemoryServiceBusState` and `ProcessErrorAsync`.
  - slice: S02
    provides: An internal-ready package surface with explicit NuGet metadata and packaged README guidance that the external consumer can follow without repo-only assumptions.
affects:
  - M002 milestone validation and closeout
  - Any downstream slice or consumer documentation that depends on package-only installation/use proof
key_files:
  - samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj
  - samples/Azure.InMemory.ExternalConsumer/NuGet.Config
  - samples/Azure.InMemory.ExternalConsumer/README.md
  - samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs
  - scripts/verify-s03-external-consumer.sh
  - src/Azure.InMemory/Azure.InMemory.csproj
  - README.md
  - .gsd/PROJECT.md
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Keep the committed consumer harness outside `Azure.InMemory.sln`, disable Central Package Management locally, and require `Azure.InMemory` to flow only through `PackageReference` so the proof cannot silently collapse back to a project-coupled build.
  - Use the package README seam in the external tests: register with `AddAzureServiceBusInMemory()`, drive behavior through `IAzureServiceBusFactory`, and use `InMemoryServiceBusState` only as the public inspection surface for retry/completion assertions.
  - Adopt a single authoritative verifier that repacks the library, recreates the consumer cache, restores through the sample-local feed, runs the focused consumer tests, and then reruns the producer solution to avoid stale-cache false positives.
patterns_established:
  - Package-only proof pattern: keep the external consumer out of the producer solution, clear inherited package sources with a sample-local `NuGet.Config`, disable local CPM, and restore into an isolated sample-local packages directory.
  - Consumer-boundary behavior proof pattern: resolve `IAzureServiceBusFactory` from `AddAzureServiceBusInMemory()`, declare topology explicitly, and assert retry/completion outcomes through the public `InMemoryServiceBusState` surface rather than repo-only helpers.
  - Authoritative verification pattern for same-version packages: repack first, recreate the consumer cache, restore with `--no-cache`, run the focused consumer tests, and then run the full producer regression suite.
observability_surfaces:
  - The packaged `InMemoryServiceBusState` surface was exercised successfully from the external consumer boundary for pending, completed, dead-lettered, and errored queue outcomes, including `DeliveryCount` / `MaxDeliveryCount` assertions across an explicit second `StartProcessingAsync()` run.
  - `ProcessErrorAsync` callbacks and staged verifier log output (`==> Pack`, `==> Restore`, `==> Run focused external consumer package proof`, `==> Run producer solution regression suite`) provide direct failure localization when the consumer package proof breaks.
drill_down_paths:
  - .gsd/milestones/M002/slices/S03/tasks/T01-SUMMARY.md
  - .gsd/milestones/M002/slices/S03/tasks/T02-SUMMARY.md
  - .gsd/milestones/M002/slices/S03/tasks/T03-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-31T12:55:18.105Z
blocker_discovered: false
---

# S03: External consumer package proof

**S03 proved the packed Azure.InMemory artifact can be restored and used from a committed package-only consumer boundary, with external xUnit coverage that exercises the documented Service Bus seam and passes through a local NuGet flow.**

## What Happened

This slice closed the producer-to-package-to-consumer loop instead of stopping at `dotnet pack`. The sample consumer under `samples/Azure.InMemory.ExternalConsumer/` stays outside `Azure.InMemory.sln`, disables repo-root Central Package Management locally, and references `Azure.InMemory` only through `<PackageReference Include="Azure.InMemory" Version="1.0.0" />` so the proof depends on the packed artifact rather than project coupling. The consumer suite uses the public README seam exactly the way another team would: `AddAzureServiceBusInMemory()` registers the package, `IAzureServiceBusFactory` declares queue topology, creates a sender, and creates a queue processor, and `InMemoryServiceBusState` is used only as the package's public test inspection surface. The main proof deliberately fails the first processor run, confirms `ProcessErrorAsync` and the errored bucket record that failure, asserts the message is still pending with `DeliveryCount == 2`, then reruns `StartProcessingAsync()` and proves completion happens on that explicit second run. The suite also adds package-boundary negative coverage for undeclared topology guidance and wrong-queue processing so the slice proves more than a compile-only smoke test. To keep the result repeatable, `scripts/verify-s03-external-consumer.sh` became the authoritative verifier: it repacks the current library into `./artifacts/pack`, recreates the sample-local package cache, restores the consumer through its local `NuGet.Config` with `--no-cache`, runs the focused external-consumer tests, and then reruns `dotnet test ./Azure.InMemory.sln` as the producer regression guard. During closeout I re-ran that end-to-end verifier successfully and also inspected the emitted `.nupkg` directly to confirm it still contains `README.md`, the net10.0 library assembly, and the package metadata/readme markers the consumer docs depend on.

## Verification

Slice-level verification passed with `bash ./scripts/verify-s03-external-consumer.sh` (exit 0, 27.5s). That run repacked `src/Azure.InMemory/Azure.InMemory.csproj`, recreated `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages`, restored the consumer via `./samples/Azure.InMemory.ExternalConsumer/NuGet.Config --no-cache`, ran the focused external-consumer suite (`Passed: 3, Failed: 0`), and reran `dotnet test ./Azure.InMemory.sln` (`Passed: 74, Failed: 0`). I also inspected `artifacts/pack/Azure.InMemory.1.0.0.nupkg` directly with `python3` and confirmed the package contains `README.md`, `lib/net10.0/Azure.InMemory.dll`, nuspec metadata for id/version/authors/license/project URL/repository, and README markers for `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, `StartProcessingAsync()`, `InMemoryServiceBusState`, and canonical `<topic>/Subscriptions/<subscription>` guidance.

## Requirements Advanced

- R003 — The external consumer harness and end-to-end verifier extend the in-process `dotnet test` proof across a real package restore boundary, showing the supported Service Bus scenario still runs locally without Azure, Docker, or other external infrastructure even when consumed through a `.nupkg`.
- R022 — S03 materially advances NuGet publication readiness by proving intentional package metadata/readme packaging, package-only restore, and meaningful external consumption from a local feed, while still leaving remote publication/release automation deferred.

## Requirements Validated

None.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

None.

## Known Limitations

The proof stays at an internal/local-feed boundary: it does not publish to a remote feed, validate release automation, or broaden package-consumer runtime coverage beyond the Service Bus seam exercised by the external xUnit sample. The sample also pins the baseline package version `1.0.0`, so truthful same-version verification depends on the repack-plus-isolated-cache pattern rather than version bumps.

## Follow-ups

Milestone validation should decide whether later work needs remote-feed/publish automation or broader packaged-consumer coverage for Blob and Key Vault, but no additional implementation slice is required to satisfy the current S03 acceptance target.

## Files Created/Modified

- `samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj` — Defines the standalone package-only external consumer test project, disables local Central Package Management, and keeps `Azure.InMemory` referenced strictly through `PackageReference`.
- `samples/Azure.InMemory.ExternalConsumer/NuGet.Config` — Clears inherited package sources and points restore explicitly at the repo-local pack output plus nuget.org so the consumer proof uses the intended local feed.
- `samples/Azure.InMemory.ExternalConsumer/README.md` — Documents the sample's guardrails, deterministic restore flow, and the single-command slice verifier for future reruns and troubleshooting.
- `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs` — Adds the external-consumer xUnit proof for queue redelivery, actionable undeclared-topology failure guidance, and wrong-queue isolation using only the packaged Service Bus seam and public inspection state.
- `scripts/verify-s03-external-consumer.sh` — Implements the authoritative pack/restore/test verification loop with stage logging, isolated cache recreation, and producer regression coverage.
- `src/Azure.InMemory/Azure.InMemory.csproj` — Continues supplying the packaged metadata/readme surface consumed by the local-feed proof and direct `.nupkg` inspection.
- `README.md` — Provides the package-facing quickstart and inspection-surface guidance that the external consumer follows and that remain embedded in the emitted package.
- `.gsd/PROJECT.md` — Updated project state to reflect that all planned M002 slices are now delivered and the next step is milestone validation/closeout.
- `.gsd/KNOWLEDGE.md` — Recorded the same-version local-feed verification lesson about repacking and recreating the sample-local package cache before restore.
