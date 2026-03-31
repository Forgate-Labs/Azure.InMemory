---
id: M002
title: "Fidelity and packaging — Context Draft"
status: complete
completed_at: 2026-03-31T13:03:47.440Z
key_decisions:
  - Keep deeper Service Bus fidelity in InMemoryServiceBusState and preserve deterministic behavior by modeling retries only on the next explicit StartProcessingAsync() run instead of adding background polling or hidden timers.
  - Apply retry and dead-letter bookkeeping on canonical <topic>/Subscriptions/<subscription> entity paths so topic fan-out clones remain isolated per subscription.
  - Use the root README.md as the single authoritative package-facing quickstart and pack that exact file into the Azure.InMemory .nupkg with explicit package identity and baseline version metadata.
  - Keep the external consumer proof outside Azure.InMemory.sln, require Azure.InMemory to flow only through PackageReference plus a sample-local NuGet feed, and use one repack→restore→consumer-test→producer-test verifier as the authoritative boundary check.
key_files:
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs
  - src/Azure.InMemory/Azure.InMemory.csproj
  - README.md
  - artifacts/pack/Azure.InMemory.1.0.0.nupkg
  - samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj
  - samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs
  - samples/Azure.InMemory.ExternalConsumer/NuGet.Config
  - scripts/verify-s03-external-consumer.sh
lessons_learned:
  - In this worktree, the authoritative verification path is foreground `bash` with explicit relative paths like `./Azure.InMemory.sln`; path-sensitive background wrappers can mis-resolve worktree-local commands even when the same command succeeds interactively.
  - Same-version package readiness needs both direct `.nupkg` inspection and an isolated consumer cache reset before restore; otherwise stale local NuGet artifacts can mask README or metadata drift.
  - The public `InMemoryServiceBusState` inspection surface plus the literal canonical subscription entity path `<topic>/Subscriptions/<subscription>` is sufficient to prove retry/completion behavior from an external consumer boundary without widening the production registration/factory seam.
---

# M002: Fidelity and packaging — Context Draft

**M002 raised Azure.InMemory’s Service Bus fidelity and proved the library can be packed, restored, and exercised through a package-only external consumer flow without widening the deterministic in-process seam.**

## What Happened

M002 delivered the milestone vision in three connected slices and re-verified the assembled result at closeout. S01 deepened the existing Service Bus seam in `InMemoryServiceBusState`/`InMemoryServiceBusFactory` so queue and canonical-subscription deliveries now advance `DeliveryCount`, preserve `MaxDeliveryCount`, requeue only for the next explicit `StartProcessingAsync()` run, and automatically dead-letter on max-delivery exhaustion while keeping errored/pending/dead-lettered state inspectable. S02 turned the library into an intentional internal-ready package surface by adding explicit NuGet metadata and a baseline `1.0.0` version to `src/Azure.InMemory/Azure.InMemory.csproj`, packing the authoritative root `README.md` into the `.nupkg`, and capturing direct package-inspection proof that the emitted artifact really contains the expected nuspec/readme surface. S03 then closed the consumer boundary by keeping a committed xUnit sample outside `Azure.InMemory.sln`, restoring `Azure.InMemory` only through `PackageReference` plus a sample-local `NuGet.Config`, and proving package-only use of `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, and `InMemoryServiceBusState` in meaningful queue redelivery tests.

Milestone closeout re-verified the assembled work instead of relying only on slice-local claims. `git diff --stat HEAD $(git merge-base HEAD main) -- ':!.gsd/'` showed substantial non-`.gsd/` changes across source, tests, packaging, sample-consumer, and verifier-script surfaces. All slice and task summaries exist under `.gsd/milestones/M002/...`, and the authoritative end-to-end command `bash ./scripts/verify-s03-external-consumer.sh` passed from this worktree: it repacked `Azure.InMemory.1.0.0.nupkg`, restored the external consumer through the sample-local feed with no cache, passed the focused external consumer proof (`Passed: 3`), and then reran the producer solution regression suite (`Passed: 74`). That closes the cross-slice boundary: the redelivery fidelity added in S01 is documented and packaged by S02 and then consumed successfully from outside the producer project in S03.

## Decision Re-evaluation

| Decision | Status | Evidence from delivered work | Revisit next milestone? |
| --- | --- | --- | --- |
| Keep deeper Service Bus fidelity in `InMemoryServiceBusState` while leaving `InMemoryServiceBusFactory` thin and deterministic over explicit processor runs. | Still valid | S01 delivered observable retry/dead-letter behavior without introducing background loops, and the milestone-level regression loop stayed green. | No |
| Reuse canonical `<topic>/Subscriptions/<subscription>` entity paths so each subscription clone retries/exhausts independently. | Still valid | S01 proved isolated subscription retry bookkeeping, S02 documented the literal path at the package boundary, and S03 relied on the same documented seam externally. | No |
| Pack the authoritative root `README.md` into the `.nupkg` and keep package identity/version explicit in the project file. | Still valid | S02 package inspection proved the emitted package contains the intended nuspec/readme surface, and S03 consumed the package through that documented quickstart. | No |
| Keep external-consumer proof outside the producer solution with package-only restore plus a single repack→restore→test verifier. | Still valid | `scripts/verify-s03-external-consumer.sh` passed at milestone closeout and localized the producer/package/consumer boundary clearly. | No |

## Horizontal Checklist

No separate horizontal checklist was present in the inlined M002 roadmap context; milestone verification therefore focused on code-change presence, success criteria, slice completion, summary availability, and cross-slice integration proof.

## Success Criteria Results

- ✅ **Success criterion 1 — Deliver one narrow but realistic Service Bus fidelity upgrade through the existing explicit seam.** S01 added observable queue and canonical-subscription redelivery fidelity without a seam rewrite: failed deliveries are requeued only on the next explicit `StartProcessingAsync()` run, `DeliveryCount` is surfaced to handlers and preserved in inspection state, and messages automatically dead-letter at `MaxDeliveryCount`. Evidence: S01 verification passed for the focused redelivery suite (`dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests`, 6 tests), broader Service Bus regression suite (44 tests), and full solution regression suite (74 tests).
- ✅ **Success criterion 2 — Make the library internal-ready as a package with truthful package-facing documentation.** S02 added intentional package identity/metadata plus a packaged root `README.md`, then captured direct `.nupkg` inspection proof in `artifacts/pack/package-inspection.txt`. Evidence: `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` passed, the emitted `artifacts/pack/Azure.InMemory.1.0.0.nupkg` exists, and direct package inspection asserted README presence plus nuspec metadata and quickstart markers.
- ✅ **Success criterion 3 — Prove a fresh external consumer can restore and use the packed library outside the producer boundary.** S03 delivered `samples/Azure.InMemory.ExternalConsumer` outside `Azure.InMemory.sln`, restored it through a sample-local `NuGet.Config`, and verified package-only queue redelivery behavior using `AddAzureServiceBusInMemory()` and `IAzureServiceBusFactory`. Milestone closeout re-ran the authoritative verifier `bash ./scripts/verify-s03-external-consumer.sh`, which repacked the library, restored the external consumer with `--no-cache`, passed the focused external consumer suite (`Passed: 3`), and reran the producer regression suite (`Passed: 74`).

## Definition of Done Results

- ✅ **All roadmap slices are complete.** The inlined roadmap marks S01, S02, and S03 as done, and `gsd_complete_milestone` validated slice completion before rendering the milestone summary.
- ✅ **All slice summaries exist.** File-system verification found `.gsd/milestones/M002/slices/S01/S01-SUMMARY.md`, `.gsd/milestones/M002/slices/S02/S02-SUMMARY.md`, `.gsd/milestones/M002/slices/S03/S03-SUMMARY.md`, plus all ten task summaries under the slice `tasks/` directories.
- ✅ **The milestone produced real non-planning code/artifact changes.** `git diff --stat HEAD $(git merge-base HEAD main) -- ':!.gsd/'` showed substantial source, test, sample, packaging, and script changes outside `.gsd/`.
- ✅ **Cross-slice integration points work together.** The milestone-level verifier `bash ./scripts/verify-s03-external-consumer.sh` proved the S01 redelivery behavior packaged in S02 is consumable by the S03 external sample and still preserves a green producer regression run.
- ✅ **Operational verification remains green.** The milestone closeout verifier completed with a fresh pack, no-cache external restore, focused consumer proof, and full producer regression pass.
- ℹ️ **Horizontal checklist.** No separate horizontal checklist was present in the inlined roadmap context, so no unchecked horizontal items remain to report.

## Requirement Outcomes

- **R020 — Deferred → Validated.** M002 delivered the first deferred advanced Service Bus fidelity slice by adding observable queue and canonical-subscription redelivery, delivery-count progression, and max-delivery dead-letter behavior while preserving the deterministic in-process seam. Evidence: S01’s focused redelivery tests, broader Service Bus regression coverage, full `dotnet test ./Azure.InMemory.sln` pass, and the milestone-level rerun of the producer regression suite after the external-consumer verification loop.
- **R022 — Deferred → Validated.** M002 delivered publication-readiness proof by giving the library intentional package identity, packing the authoritative README into the `.nupkg`, and then proving a fresh external consumer can restore and use the package from a local NuGet flow. Evidence: S02’s direct `.nupkg` inspection proof plus S03’s package-only consumer restore/use flow, revalidated at milestone closeout by `bash ./scripts/verify-s03-external-consumer.sh` (`Passed: 3` external tests, `Passed: 74` producer tests).
- No other requirement status transitions were introduced during M002.

## Deviations

Product scope stayed aligned with the roadmap. The only meaningful deviation at closeout was verification-harness related: worktree-local script/solution paths were resolved reliably with foreground `bash`, so the authoritative milestone verification used `bash ./scripts/verify-s03-external-consumer.sh` rather than a background wrapper invocation.

## Follow-ups

Next milestone work should build on the now-proven package boundary and deterministic Service Bus seam: pursue deeper fidelity only where it preserves the explicit processor model (or clearly re-scope that model first), and treat public-package publication, broader Service Bus parity features (for example sessions, lock renewal, scheduling, or duplicate detection), and richer consumer examples as separate, explicitly verified increments.
