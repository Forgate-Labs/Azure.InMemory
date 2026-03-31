---
id: M001
title: "Core in-memory Azure providers"
status: complete
completed_at: 2026-03-30T22:53:23.462Z
key_decisions:
  - Use explicit resource-specific registrations (`AddAzure*Sdk()` / `AddAzure*InMemory()`) instead of a single global provider mode.
  - Keep public seams resource-specific and library-owned (`IAzureServiceBusFactory`, `IAzureBlobFactory`, `IAzureKeyVaultFactory`) rather than returning raw Azure SDK clients.
  - Adapt host-registered Azure SDK clients through DI activation lambdas so SDK-backed mode shares the same seam and fails with actionable missing-client diagnostics.
  - Back each in-memory provider with a singleton shared-state root that doubles as the test observability surface.
  - Model Service Bus topic publishes on canonical `<topic>/Subscriptions/<subscription>` paths and keep processor lifecycle transitions in shared state.
  - Drain the current pending Service Bus batch synchronously during processor runs so settlement behavior stays deterministic and inspectable.
  - Keep Blob and Key Vault runtime proof in dedicated behavior suites while leaving provider registration tests focused on DI composition and diagnostics.
key_files:
  - Azure.InMemory.sln
  - Directory.Build.props
  - Directory.Packages.props
  - src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs
  - src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs
  - src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs
  - tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs
  - tests/Azure.InMemory.Tests/KeyVault/InMemory/InMemoryKeyVaultBehaviorTests.cs
  - tests/Azure.InMemory.Tests/Blob/InMemory/InMemoryBlobBehaviorTests.cs
lessons_learned:
  - A thin public seam plus rich test-only shared state is enough to make in-memory Azure behavior both truthful and highly assertable.
  - Dedicated behavior suites per resource produce clearer capability proof than overloading provider registration tests with runtime behavior assertions.
  - Canonical Service Bus subscription paths are the right stable unit for both ingress and processor observability; treating the topic name itself as a queue would have created false semantics.
  - When earlier slices deliver working behavior honestly, later slices can focus on proof location and traceability rather than widening APIs just to match the original roadmap wording.
  - A single authoritative `dotnet test ./Azure.InMemory.sln` run is the cleanest milestone-level evidence because parallel test runs can contend on shared `bin/obj` outputs.
---

# M001: Core in-memory Azure providers

**M001 delivered explicit SDK-vs-in-memory Azure provider seams plus verified in-memory Service Bus, Blob, and Key Vault behavior that runs entirely inside `dotnet test`.**

## What Happened

M001 turned the initial design into a buildable `net10.0` library and test solution that behaves like an Azure-focused equivalent of EF Core InMemory without pretending to clone the full Azure SDK surface. S01 established explicit per-resource registrations (`AddAzureServiceBusSdk()` / `AddAzureServiceBusInMemory()` and Blob/Key Vault equivalents), focused factory seams, SDK adapters over DI-registered Azure clients, and singleton in-memory state roots. S02 then made Service Bus ingress truthful by requiring declared topology, preserving message metadata on queue sends, and fanning topic publishes into canonical `<topic>/Subscriptions/<subscription>` pending paths. S03 completed the Service Bus MVP by implementing processor execution over the current pending batch plus inspectable completed, dead-lettered, pending, and errored outcomes keyed by queue or canonical subscription path. S04 and S05 closed the remaining traceability gaps with dedicated Key Vault and Blob behavior suites so runtime proof now lives in focused in-memory behavior tests while provider registration tests stay DI-focused.

Milestone-level verification confirmed the slices assemble into one coherent result rather than five isolated proofs. `git diff --stat HEAD $(git merge-base HEAD main) -- ':!.gsd/'` showed 29 non-`.gsd/` files changed, so the milestone produced real code rather than planning artifacts. `find .gsd/milestones/M001 -maxdepth 3 -type f | sort` confirmed the roadmap, validation artifact, and all S01-S05 summary/UAT files exist. A fresh `dotnet test ./Azure.InMemory.sln` run passed 68/68 tests in-process with no Azure, Docker, or other external infrastructure, which validates the cross-slice integration contract: the S01 provider seam supports the S02/S03 Service Bus pipeline and the S04/S05 Blob/Key Vault proof surfaces without regressions.

## Decision Re-evaluation

| Decision | Still valid? | Evidence | Revisit next milestone? |
|---|---|---|---|
| Expose test-oriented harness/inspection APIs beyond the official SDK surface (D001/D003) | Yes | S03's completed/dead-lettered/pending/errored state plus S04/S05 shared-state inspection are the core verification mechanism for meaningful tests. | No |
| Use resource-specific DI registrations instead of one omnibus provider mode (D002) | Yes | S01 mixed-provider composition tests and the final green suite prove Service Bus, Blob, and Key Vault can choose backends independently. | No |
| Adapt host-registered Azure SDK clients through DI instead of constructing clients internally (D004/D007) | Yes | SDK-backed registration tests remained explicit and actionable without credential bootstrapping inside this library. | No |
| Keep public seams resource-specific and library-owned instead of returning raw Azure SDK clients (D005) | Yes | The same seams supported truthful in-memory behavior, SDK-backed adapters, and dedicated behavior suites without widening into full SDK parity. | No |
| Keep in-memory behavior rooted in singleton shared state per resource (D006/D018/D019) | Yes | Shared state enabled truthful topology, ingress, processor lifecycle observability, blob inspection, and secret inspection across all slices. | No |
| Represent topic publishes on canonical `<topic>/Subscriptions/<subscription>` paths, never the topic name itself (D016) | Yes | S02/S03 proofs depend on this invariant, and the full suite stayed green with the processor consuming those canonical paths. | No |
| Keep Blob/Key Vault runtime proof in dedicated behavior suites while registration tests stay DI-only (D025/D028) | Yes | S04/S05 produced cleaner capability proof and preserved low-noise DI diagnostics. | No |

## Horizontal Checklist

No separate horizontal checklist was present in the milestone roadmap; no additional unchecked cross-cutting items were found during closeout.

## Success Criteria Results

- ✅ **Explicit resource-specific registration and focused factory seam delivered.** S01 established `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` plus explicit `AddAzure*Sdk()` / `AddAzure*InMemory()` registrations. `ServiceBusProviderRegistrationTests`, `BlobProviderRegistrationTests`, `KeyVaultProviderRegistrationTests`, and `MixedProviderCompositionTests` prove per-resource backend selection, conflict guards, and SDK-backed adapter resolution.
- ✅ **In-memory Service Bus topology and ingress are truthful and infrastructure-free.** S02's `InMemoryServiceBusIngressTests` prove queues/topics/subscriptions must be declared first, queue sends preserve body/`MessageId`/application properties, and topic publishes fan out into canonical subscription paths entirely inside `dotnet test`.
- ✅ **In-memory Service Bus processor execution and settlement observability are delivered.** S03 added processor execution over the current pending batch plus inspectable completed, dead-lettered, pending, and errored outcomes. Decision evidence D021, D022, and D023 records that `CompleteMessageAsync`, `DeadLetterMessageAsync`, and test-only observability surfaces were validated by processor-focused and broader regression runs.
- ✅ **In-memory Key Vault basic set/get works through the public seam.** S04's `InMemoryKeyVaultBehaviorTests` prove `AddAzureKeyVaultInMemory()` + `IAzureKeyVaultFactory` support secret round trips, missing-secret `null`, case-insensitive overwrite/latest-version behavior, and fail-fast validation with full-solution compatibility.
- ✅ **In-memory Blob basic upload/download/exists works through the public seam.** S05's `InMemoryBlobBehaviorTests` prove blob round trips, missing-blob `false`/`null`, overwrite semantics, case-insensitive identity, cloned snapshots, preserved `contentType`, and `GetContainer(...)` namespace establishment with full-solution compatibility.
- ✅ **Supported scenarios stay green in the in-process `dotnet test` loop.** A fresh milestone-closeout run of `dotnet test ./Azure.InMemory.sln` passed 68/68 tests, and D030 records that the supported M001 scenarios execute fully in-process with no external infrastructure.

## Definition of Done Results

- ✅ **All roadmap slices are complete.** The inlined roadmap marks S01-S05 as done, and the closeout directory scan found plan/research/summary/UAT artifacts for every slice.
- ✅ **All slice summaries exist.** `find .gsd/milestones/M001 -maxdepth 3 -type f | sort` returned `S01-SUMMARY.md` through `S05-SUMMARY.md` together with the milestone roadmap and validation artifact.
- ✅ **The milestone contains real code, not only planning artifacts.** `git diff --stat HEAD $(git merge-base HEAD main) -- ':!.gsd/'` reported 29 non-`.gsd/` files changed, including library source, tests, and solution/build configuration.
- ✅ **Cross-slice integration works.** The S01 seam underpins S02/S03 Service Bus execution and S04/S05 Blob/Key Vault behavior proof, and a fresh `dotnet test ./Azure.InMemory.sln` run passed 68/68 tests across the assembled solution.
- ✅ **Verification evidence exists at both slice and milestone level.** Each slice summary records focused verification commands, and milestone closeout added an authoritative full-solution regression run plus code-diff and artifact-presence checks.

## Requirement Outcomes

- **R001:** Active → Validated. Evidence: S01 registration and mixed-composition tests prove each resource can choose SDK or in-memory registration independently via explicit `AddAzure*` methods.
- **R002:** Active → Validated. Evidence: S01 exposes focused `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` seams and verifies them directly in DI/composition coverage.
- **R003:** Active → Validated. Evidence: D030 plus the milestone closeout `dotnet test ./Azure.InMemory.sln` run (68/68 passed) prove supported M001 scenarios execute in-process with no Azure, Docker, or other external infrastructure.
- **R004:** Active → Validated. Evidence: S01 SDK-backed factories adapt DI-registered official Azure clients behind the same seams, and registration tests prove the SDK-backed seams resolve correctly.
- **R005:** Active → Validated. Evidence: S02 `InMemoryServiceBusIngressTests` prove queues/topics/subscriptions can be declared in memory and messages become observable on the correct queue or canonical subscription paths.
- **R006:** Active → Validated. Evidence: S03 processor execution tests prove `CompleteMessageAsync` and `DeadLetterMessageAsync` move envelopes into inspectable completed and dead-lettered outcome stores on declared entity paths.
- **R007:** Active → Validated. Evidence: S03 exposes and tests pending, completed, dead-lettered, and errored inspection surfaces with actionable diagnostics for undeclared topology and invalid settlement ordering.
- **R008:** Active → Validated. Evidence: S04 `InMemoryKeyVaultBehaviorTests` prove `SetSecretAsync` / `GetSecretAsync` through `AddAzureKeyVaultInMemory()` and `IAzureKeyVaultFactory` with no external infrastructure.
- **R009:** Active → Validated. Evidence: S05 `InMemoryBlobBehaviorTests` prove upload/download/exists through `AddAzureBlobInMemory()` and `IAzureBlobFactory`, including preserved `contentType`, overwrite behavior, and cloned snapshots.
- **R010:** Active → Validated. Evidence: S03, S04, and S05 rely on explicit test-only inspection surfaces (`InMemoryServiceBusState`, `InMemoryKeyVaultState`, `InMemoryBlobState`) beyond the official SDK surface to make outcomes assertable.

No active requirement was invalidated or re-scoped during M001. Deferred and out-of-scope requirements remain unchanged.

## Deviations

S04 and S05 became verification-and-closeout slices rather than new implementation slices because S01 had already shipped truthful basic Blob and Key Vault behavior. This was a net positive: the milestone met scope without unnecessary API churn, and the later slices narrowed their work to dedicated proof surfaces. Verification and closeout also consistently used the explicit relative solution path `./Azure.InMemory.sln` for reliable worktree-local commands.

## Follow-ups

Start M002 by preserving M001's explicit seams and observability contracts while deepening deferred fidelity: advanced Service Bus semantics (for example retries and delivery count), Azure Functions-style blob-trigger integration, and overall packaging/NuGet readiness. Keep the dedicated behavior-suite pattern for runtime proof and retain canonical `<topic>/Subscriptions/<subscription>` paths as the Service Bus processing invariant.
