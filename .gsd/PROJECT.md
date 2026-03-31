# Project

## What This Is

A .NET library for tests that provides in-memory Azure resource backends for Service Bus, Blob Storage, and Key Vault. Projects choose between official Azure SDK-backed providers and in-memory providers through explicit registration and focused factories, so tests can run with `dotnet test` without Azure, Docker, or other external infrastructure.

## Core Value

The one thing that must work even if everything else is cut: a test can switch from real Azure wiring to in-memory wiring and still execute a meaningful integration flow locally, especially the Service Bus processor path.

## Current State

Milestones M001 and M002 are complete. The repository contains a buildable `net10.0` solution, explicit SDK vs in-memory registration extensions for Service Bus, Blob, and Key Vault, focused factory seams for each resource, and infrastructure-free verification tests that keep the supported scenarios green under `dotnet test`. Service Bus now supports explicit in-memory queue/topic/subscription topology creation, truthful queue ingress and topic fan-out into canonical `<topic>/Subscriptions/<subscription>` paths, synchronous processor execution over the current pending batch, inspectable completed, dead-lettered, pending, and errored outcomes with actionable diagnostics, plus deterministic redelivery fidelity: failed queue and subscription deliveries are requeued only for the next explicit `StartProcessingAsync()` run, `DeliveryCount` is surfaced to handlers and preserved in pending/dead-letter/errored outcomes, and messages automatically dead-letter once the configured `MaxDeliveryCount` is exhausted without duplicating sibling subscription copies or introducing background polling. Blob basics remain proven by a dedicated in-memory behavior suite that exercises `AddAzureBlobInMemory()` and `IAzureBlobFactory` for upload/download/exists round trips, missing-blob `false`/`null` behavior, overwrite conflict vs replacement semantics, case-insensitive container/blob identity, cloned content snapshots with preserved `contentType`, and the `GetContainer(...)` namespace-establishing convention. Key Vault basics are likewise proven by a dedicated in-memory behavior suite that exercises `SetSecretAsync`/`GetSecretAsync` through `IAzureKeyVaultFactory`, covers missing-secret null behavior, case-insensitive overwrite/latest-version semantics, and shared `InMemoryKeyVaultState` inspection. The package surface is now intentional enough for internal consumption: `src/Azure.InMemory/Azure.InMemory.csproj` carries explicit NuGet metadata and a baseline `1.0.0` version, the authoritative root `README.md` is packed into the `.nupkg`, the committed external-consumer xUnit sample restores through its own `NuGet.Config` and isolated package cache, and `scripts/verify-s03-external-consumer.sh` repacks the library, restores the package-only consumer from the local feed, runs the consumer redelivery proof, and reruns `dotnet test ./Azure.InMemory.sln` as the producer regression guard. All M001 requirements R001-R010 remain validated, M002 has now validated both R020 (advanced Service Bus fidelity) and R022 (NuGet publication readiness), and the next project action is to choose and plan the next milestone rather than finish pending M002 work.

## Architecture / Key Patterns

The project uses resource-specific provider registration and focused factories rather than trying to clone the Azure SDK surface wholesale. Each resource gets two registrations: one for official SDK-backed behavior and one for in-memory behavior. The public composition pattern is intentionally explicit and verbose, e.g. `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()`, so consumers can inject only what they need. The in-memory providers also expose extra test harness and inspection surfaces beyond the operational contract so tests can assert what happened.

## Capability Contract

See `.gsd/REQUIREMENTS.md` for the explicit capability contract, requirement status, and coverage mapping.

## Milestone Sequence

- [x] M001: Core in-memory Azure providers — Establish the provider seam and deliver useful in-memory Service Bus, Blob, and Key Vault support for local tests.
- [x] M002: Fidelity and packaging — Deepen behavioral fidelity, add deferred scenarios, and prepare the library for NuGet publication when the implementation level is good enough.
