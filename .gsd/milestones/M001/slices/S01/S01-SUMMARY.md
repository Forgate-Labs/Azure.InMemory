---
id: S01
parent: M001
milestone: M001
provides:
  - (none)
requires:
  []
affects:
  - S02
  - S03
  - S04
  - S05
key_files:
  - Azure.InMemory.sln
  - Directory.Build.props
  - Directory.Packages.props
  - src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs
  - src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs
  - src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs
  - src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs
  - src/Azure.InMemory/Blob/IAzureBlobFactory.cs
  - src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs
  - src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs
  - src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs
  - src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs
  - src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs
key_decisions:
  - Kept the public seam resource-specific via `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` instead of introducing a single omnibus provider abstraction.
  - Resolved SDK-backed factories from DI-registered Azure clients through activation lambdas so missing client prerequisites fail at factory resolution with actionable resource-specific messages.
  - Standardized each resource registration on per-resource markers plus `TryAddSingleton`, making repeated same-mode registrations idempotent while failing fast on mixed same-resource backends.
  - Shipped Blob and Key Vault in-memory seams with real shared-state basic behavior now, rather than leaving registration-only stubs for later slices.
patterns_established:
  - Use one explicit `AddAzure<Resource>Sdk()` / `AddAzure<Resource>InMemory()` pair per resource instead of a global provider mode switch.
  - Keep the public seam library-owned and resource-scoped, then adapt official Azure clients behind that seam rather than leaking raw SDK clients to consumers.
  - Back each in-memory provider with a singleton shared state root that the focused factory and tests both resolve from DI.
  - Treat same-resource mixed backend registration as a configuration error, but allow different resources to choose different backends in the same host.
observability_surfaces:
  - `InMemoryServiceBusState` exposes queue/topic/subscription existence and pending envelope inspection that downstream Service Bus slices can build on.
  - `InMemoryBlobState` exposes container/blob existence and stored blob content for infrastructure-free verification.
  - `InMemoryKeyVaultState` exposes secret existence and stored records, including generated secret versions.
  - Missing-client and same-resource conflict paths now surface explicit, resource-specific `InvalidOperationException` messages that make DI misconfiguration failures immediately diagnosable.
drill_down_paths:
  - .gsd/milestones/M001/slices/S01/tasks/T01-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T02-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T03-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T04-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T05-SUMMARY.md
duration: ""
verification_result: passed
completed_at: 2026-03-30T21:16:45.633Z
blocker_discovered: false
---

# S01: Provider registration and focused factories

**Established explicit per-resource SDK vs in-memory registrations, focused factories, shared in-memory state roots, and green infrastructure-free composition tests for Service Bus, Blob, and Key Vault.**

## What Happened

S01 turned the empty project plan into a buildable `net10.0` Azure.InMemory solution with a stable resource-specific composition seam. The slice established three focused public factories—Service Bus, Blob, and Key Vault—plus their supporting companion abstractions so later slices can add behavior without widening into a fake full Azure SDK. It also locked shared build/package policy in central props files and seeded the verification surface with concrete registration tests rather than leaving the seam implicit.

For Service Bus, the slice implemented explicit `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()` registrations, an SDK-backed adapter over DI-registered `ServiceBusClient` and `ServiceBusAdministrationClient`, and an in-memory factory rooted in a singleton `InMemoryServiceBusState`. That state already tracks queues, topics, subscriptions, and pending envelopes, and the seam now includes sender, processor, administration, and message/error context contracts sized for the later ingress and processor slices. The in-memory processor deliberately stops at an explanatory `NotSupportedException`, which keeps the seam honest while preserving the API shape S02/S03 need.

For Blob and Key Vault, S01 went further than pure registration plumbing. The slice added explicit SDK vs in-memory registrations, the same fail-fast conflict-guard pattern used for Service Bus, and truthful in-memory state roots that already support the milestone’s basic read/write flows. `InMemoryBlobState` stores cloned `BinaryData` payloads and content types behind the `IAzureBlobFactory` seam, while `InMemoryKeyVaultState` stores latest secret values plus generated versions behind `IAzureKeyVaultFactory`. The targeted tests prove upload/download/exists and set/get behavior without Azure, Docker, or any other external infrastructure.

T05 closed the slice by replacing the mixed-provider placeholder with real composition tests that combine different backends per resource in one `IServiceCollection`. Those tests prove the central S01 promise: each resource can choose SDK or in-memory independently, the resolved focused factory matches that choice, and same-resource backend conflicts fail fast with actionable messages while the other resources remain unaffected. The final full-suite verification stayed infrastructure-free and green.

## Verification

Executed the planned verification loops from the slice tasks and the final slice proof with the explicit relative solution path.

- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests` → ✅ pass, 5 tests, 3642ms wall time.
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests` → ✅ pass, 5 tests, 3982ms wall time.
- `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests` → ✅ pass, 5 tests, 3611ms wall time.
- `dotnet test ./Azure.InMemory.sln` → ✅ pass, 20 tests, 3600ms wall time.

The passing suite proves explicit SDK vs in-memory registration for each resource, singleton shared-state reuse, actionable missing-client failures, same-resource conflict guards, mixed-resource backend composition, and infrastructure-free basic Blob/Key Vault flows under `dotnet test`.

## Requirements Advanced

- R003 — Established and verified the infrastructure-free `dotnet test` loop for every scenario currently delivered in S01, creating the invariant that later slices must preserve as Service Bus behavior deepens.
- R005 — Implemented `InMemoryServiceBusState` plus queue/topic/subscription creation and sender enqueue buffering, providing the topology and ingress seam that S02 will exercise.
- R006 — Sized the processor contract and message/error contexts for settlement work, while leaving an explicit `NotSupportedException` in the in-memory processor until truthful execution lands in S03.

## Requirements Validated

- R001 — `ServiceBusProviderRegistrationTests`, `BlobProviderRegistrationTests`, `KeyVaultProviderRegistrationTests`, and `MixedProviderCompositionTests` prove each resource can register SDK or in-memory backends independently via explicit AddAzure* methods.
- R002 — The slice exposes focused `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` seams and verifies them directly in the DI and composition tests.
- R004 — `AzureServiceBusSdkFactory`, `AzureBlobSdkFactory`, and `AzureKeyVaultSdkFactory` resolve over DI-registered official Azure clients, and the registration tests prove the SDK-backed seams are selectable and usable.
- R008 — `AddAzureKeyVaultInMemorySupportsBasicSetAndGetFlowAgainstTheSharedState` proves a test can set and read secrets through the in-memory Key Vault seam with generated versions and no external infrastructure.
- R009 — `AddAzureBlobInMemorySupportsBasicUploadDownloadAndExistsFlowAgainstTheSharedState` proves a test can write and read blobs through the in-memory Blob seam with preserved content type and no external infrastructure.

## New Requirements Surfaced

None.

## Requirements Invalidated or Re-scoped

None.

## Deviations

- Used `./Azure.InMemory.sln` instead of the bare solution name for reliable worktree-local shell verification.
- S01 delivered truthful in-memory Blob and Key Vault basic read/write behavior earlier than the original roadmap implied, so downstream scope for S04 and S05 should be reassessed rather than assuming those slices still start from registration-only seams.

## Known Limitations

- `InMemoryServiceBusFactory` establishes registration, topology state, and sender buffering, but `StartProcessingAsync()` on the in-memory processor still throws an explanatory `NotSupportedException` until S02/S03 implement truthful processing.
- The SDK adapters are intentionally narrow and sized to the focused M001 seam rather than full Azure SDK surface parity.
- Blob and Key Vault already cover the basic in-memory flows, but they do not yet expose broader test-specific inspection APIs beyond the shared state roots.

## Follow-ups

- S02 should build real in-memory Service Bus topology ingress and message distribution on top of the already-shared `InMemoryServiceBusState` and focused sender/administration seam.
- S03 should implement truthful processor execution and settlement observability behind the existing `IAzureServiceBusProcessor` contract, replacing the current explanatory `NotSupportedException` path.
- Reassess the roadmap for S04 and S05: basic Key Vault set/get and Blob upload/download/exists are already proven in S01, so those slices may be narrowed to richer behavior, extra observability, or removed if no additional scope remains.

## Files Created/Modified

- `Azure.InMemory.sln` — Created the solution root and wired the library and test projects into a stable `Azure.InMemory` solution layout.
- `Directory.Build.props` — Centralized shared .NET build settings for the new solution.
- `Directory.Packages.props` — Centralized package version management for Azure SDK and test dependencies.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — Defined the Service Bus focused seam, including sender, processor, administration, and context contracts sized for later slices.
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — Implemented explicit Service Bus SDK vs in-memory registration extensions with conflict guards and required-client diagnostics.
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — Implemented the Service Bus SDK adapter behind `IAzureServiceBusFactory`.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — Implemented the in-memory Service Bus factory and shared topology/message state root that later slices build on.
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — Defined the Blob focused seam plus truthful SDK and in-memory adapters backed by shared state.
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — Implemented explicit Blob SDK vs in-memory registration extensions with fail-fast conflict behavior.
- `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs` — Implemented the Blob SDK adapter plus in-memory blob storage state for upload/download/exists behavior.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — Implemented the singleton in-memory blob state root used by the factory and tests.
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — Defined the Key Vault focused seam plus truthful SDK and in-memory secret-store adapters.
- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` — Implemented explicit Key Vault SDK vs in-memory registration extensions with fail-fast conflict behavior.
- `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs` — Implemented the Key Vault SDK adapter and in-memory secret state with generated versions.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs` — Implemented the singleton in-memory Key Vault state root used by the factory and tests.
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs` — Replaced placeholder registration tests with Service Bus backend-selection, singleton-state, and fail-fast coverage.
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs` — Replaced placeholder registration tests with Blob backend-selection, shared-state, and basic upload/download/exists coverage.
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs` — Replaced placeholder registration tests with Key Vault backend-selection, shared-state, and basic set/get coverage.
- `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs` — Added mixed-resource composition tests proving independent backend choice per resource and same-resource conflict isolation.
- `.gsd/KNOWLEDGE.md` — Captured infrastructure-free Azure client test setup and worktree verification gotchas for future slices.
- `.gsd/PROJECT.md` — Updated the project state snapshot to reflect that S01 is implemented and verified.
