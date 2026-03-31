---
id: S02
parent: M002
milestone: M002
provides:
  - An `Azure.InMemory.1.0.0.nupkg` artifact with intentional NuGet metadata and the authoritative packaged README, ready for local-feed consumer proof.
  - A package-facing README quickstart that shows `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, explicit topology setup, deterministic rerun semantics, and test-only inspection guidance truthfully.
  - A durable package-inspection artifact that downstream S03 can use to verify producer-boundary metadata/readme drift before testing consumer restore/use.
requires:
  - slice: S01
    provides: The active M002 producer root plus the deterministic Service Bus rerun semantics and canonical subscription-path rules that S02 now documents truthfully from the package boundary.
affects:
  - S03
key_files:
  - src/Azure.InMemory/Azure.InMemory.csproj
  - README.md
  - artifacts/pack/Azure.InMemory.1.0.0.nupkg
  - artifacts/pack/package-inspection.txt
  - .gsd/DECISIONS.md
  - .gsd/KNOWLEDGE.md
  - .gsd/PROJECT.md
key_decisions:
  - D038: use the root `README.md` as the single authoritative package-facing quickstart and pack that exact file into the `.nupkg`.
  - D039: keep an explicit baseline package version of `1.0.0` in the library project so downstream local-feed proof can override it intentionally.
patterns_established:
  - Keep package-facing docs single-sourced in the root `README.md` and pack that exact file into the `.nupkg` rather than maintaining a packaging-only duplicate.
  - Treat package readiness as producer-boundary evidence: keep the full solution green, emit a fresh package artifact, and inspect the embedded `.nuspec` plus packaged `README.md` directly instead of trusting `dotnet pack` alone.
  - Use an explicit baseline package version in the project file so downstream local-feed proof can intentionally override it without relying on NuGet cache luck or implicit defaults.
observability_surfaces:
  - `artifacts/pack/package-inspection.txt` records direct producer-boundary proof that the emitted package contains `README.md`, the intended nuspec metadata, and the packaged quickstart markers another team would consume.
  - The packaged root `README.md` now documents `InMemoryServiceBusState` and the canonical `<topic>/Subscriptions/<subscription>` inspection rule from the package boundary, reducing reliance on repo-only knowledge when downstream slices assert in-memory outcomes.
drill_down_paths:
  - .gsd/milestones/M002/slices/S02/tasks/T01-SUMMARY.md
  - .gsd/milestones/M002/slices/S02/tasks/T02-SUMMARY.md
  - .gsd/milestones/M002/slices/S02/tasks/T03-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-31T03:30:57.642Z
blocker_discovered: false
---

# S02: Internal-ready package surface and docs

**Delivered an internal-ready Azure.InMemory package surface with explicit NuGet metadata, a packaged README quickstart, and direct `.nupkg` inspection proof from the producer boundary.**

## What Happened

S02 made the library feel intentional at the package boundary instead of like a default class library. `src/Azure.InMemory/Azure.InMemory.csproj` now declares real package identity and metadata (`PackageId`, explicit baseline `Version` `1.0.0`, authors/company, tags, project/repository URLs, repository type, MIT license expression, and `PackageReadmeFile`). The project also packs the authoritative root `README.md` directly into the `.nupkg`, so there is one package-facing guide instead of a repo-only document plus a packaging duplicate.

The root `README.md` was rewritten around the actual public Service Bus seam another internal team would consume. It now shows package installation, `using Azure.InMemory.DependencyInjection;`, `services.AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, explicit queue/topic/subscription topology creation, sender/processor usage, settlement rules (`CompleteMessageAsync(...)`, `DeadLetterMessageAsync(...)`, `AutoCompleteMessages: true`), deterministic rerun semantics (`StartProcessingAsync()` only retries on the next explicit run), and the test-only `InMemoryServiceBusState` inspection surface. It also states the literal canonical subscription entity-path rule `<topic>/Subscriptions/<subscription>` from the package boundary instead of assuming repo-internal helpers are available.

T03 then closed the slice with producer-boundary proof instead of trusting source edits alone. The solution regression stayed green, a fresh `artifacts/pack/Azure.InMemory.1.0.0.nupkg` was emitted, and direct zip/nuspec inspection proved the package really ships the embedded `README.md` plus the intended metadata surface. `artifacts/pack/package-inspection.txt` now captures that proof durably, including the packaged README markers S03 will rely on when it restores and uses this artifact through a local NuGet flow. This slice intentionally stops at internal-ready packaging and docs; it does not claim fresh external-consumer restore/use proof yet, which remains the acceptance boundary for S03.

## Verification

Executed the slice verification plan from the active M002 root using the explicit relative solution path `./Azure.InMemory.sln`: `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln` (✅ pass, 74/74), `DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` (✅ pass), `test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md` (✅ pass), a direct `python3` inspection of `./artifacts/pack/Azure.InMemory.1.0.0.nupkg` that asserted packaged `README.md` presence plus nuspec metadata and quickstart markers (✅ pass), and final file checks for `artifacts/pack/Azure.InMemory.1.0.0.nupkg` plus `artifacts/pack/package-inspection.txt` (✅ pass). Together these prove the producer project packs cleanly, the README exposes the real DI/factory/settlement guidance, and the emitted package actually contains the intended README and metadata surface.

## Requirements Advanced

- R022 — Advanced the deferred publication-readiness requirement by giving the library an intentional package identity, packaged README, and direct `.nupkg` inspection proof at the producer boundary, while leaving fresh external-consumer restore/use validation to S03.

## Requirements Validated

None.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

Used `python3` instead of the slice-plan `python` spelling because this WSL worktree has no `python` alias, and used foreground `bash` for the authoritative `dotnet test` verification because `async_bash` misresolved `./Azure.InMemory.sln` in this worktree. These were verification-harness adjustments only; the product scope and slice contract did not change.

## Known Limitations

S02 proves producer-boundary packaging and package-facing documentation only. It does not yet validate a fresh external consumer restoring `Azure.InMemory` from a local NuGet feed, wiring `AddAzureServiceBusInMemory()`, and passing an out-of-repo scenario; that acceptance remains owned by S03. This slice also stops short of claiming full public NuGet publication readiness beyond the internal-ready surface demonstrated here.

## Follow-ups

S03 should restore the packed artifact through a local NuGet source, follow the packaged README quickstart verbatim, and prove an out-of-producer consumer can register `AddAzureServiceBusInMemory()` and pass a meaningful test. If S03 republishes the same baseline version locally, it should keep direct `.nupkg` inspection available so stale same-version artifacts cannot hide readme or metadata drift.

## Files Created/Modified

- `src/Azure.InMemory/Azure.InMemory.csproj` — Added intentional NuGet package metadata, explicit baseline versioning, and root-README packing so the library emits a deliberate internal package instead of default sparse nuspec values.
- `README.md` — Replaced the placeholder content with a package-facing Service Bus quickstart covering installation, DI registration, explicit topology, settlement semantics, deterministic reruns, and test-only inspection guidance.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` — Fresh Release package artifact emitted from the producer project for downstream local-feed consumer proof.
- `artifacts/pack/package-inspection.txt` — Recorded direct inspection of the emitted package, including embedded README presence, nuspec metadata, and packaged quickstart markers.
- `.gsd/DECISIONS.md` — Captured the explicit baseline-version packaging decision so downstream packaging and consumer-proof work inherits the same deterministic artifact strategy.
- `.gsd/KNOWLEDGE.md` — Captured package-proof and worktree-specific verification lessons, including direct `.nupkg` inspection for same-version artifacts.
- `.gsd/PROJECT.md` — Updated the project snapshot to reflect that M002/S02 is complete and that S03 is now the remaining milestone slice.
