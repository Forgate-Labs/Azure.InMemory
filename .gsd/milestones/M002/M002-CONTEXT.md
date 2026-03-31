# M002: Fidelity and packaging — Context Draft

**Gathered:** 2026-03-30
**Status:** Ready for planning

## Project Description

Azure.InMemory is a .NET library for tests that provides in-memory Azure resource backends for Service Bus, Blob Storage, and Key Vault. Projects choose between official Azure SDK-backed providers and in-memory providers through explicit registration and focused factories, so tests can run with `dotnet test` without Azure, Docker, or other external infrastructure.

M001 already proved the seam and the first useful MVP. M002 is the follow-on milestone that should deepen the usefulness and credibility of the library without turning it into an Azure SDK clone or reopening the core architecture.

## Why This Milestone

M001 proved that the explicit seam works and that the library can already support useful local integration flows. M002 exists so the project does not stop at a thin MVP.

The current planning direction is:
- **Service Bus first** as the center of gravity for M002
- fidelity improvements before broad new surface area
- packaging as a finishing layer for a better internal consumption story, not the main point of the milestone
- **Blob Trigger deferred later**, not pulled into M002

Why now: the next thing that increases trust is not re-architecting the seam, but making the in-memory behavior catch more realistic integration mistakes and making the library feel solid enough to consume as an internal package.

## User-Visible Outcome

### When this milestone is complete, the user can:

- run a local `dotnet test` integration flow against the in-memory Service Bus provider and catch more realistic messaging behavior than the M001 basic path
- consume Azure.InMemory as an **internal-ready** package with enough docs/examples/polish that another close-in consumer project can use it without guessing

### Entry point / environment

- Entry point: consumer test projects using explicit registrations such as `AddAzureServiceBusInMemory()` / `AddAzureServiceBusSdk()` and package consumption through normal .NET/NuGet flows
- Environment: local dev, in-process `dotnet test`, plus internal package consumption verification
- Live dependencies involved: none for the in-memory test loop; optional internal package feed or local `.nupkg` consumption during packaging proof

## Completion Class

- Contract complete means: the next agreed M002 Service Bus fidelity behaviors are covered by dedicated tests and observability surfaces without breaking the explicit focused-factory seam
- Integration complete means: a consumer project can swap to the in-memory Service Bus provider, exercise the richer behavior in a real test flow, and an internal consumer can install/use the packed library successfully
- Operational complete means: pack/restore/test works through the normal local build lifecycle and the in-memory loop still runs fully inside `dotnet test` with no Azure, Docker, or emulator dependency

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- a real consumer-style Service Bus test can execute against the in-memory provider and validate a richer post-M001 messaging scenario, not just the basic send/process/complete/dead-letter path already covered in M001
- a fresh internal consumer can reference the packaged Azure.InMemory output, follow the provided docs/examples, and run a meaningful local test flow successfully
- what cannot be simulated away for this milestone is the package-consumption experience itself: M002 should not be considered truly done unless the library is actually packed and used from outside the producing project boundary in an internal-consumption-style flow

## Risks and Unknowns

- The exact **next Service Bus fidelity behaviors** are still the main gray area — this materially changes slice design, proof strategy, and whether the seam can stay lean
- Packaging can sprawl into public-distribution concerns too early — the user explicitly wants **internal-ready only**, not a forced public NuGet push
- Blob Trigger work could drag in runtime/host emulation — for now the user wants it **deferred later**, which protects M002 from scope creep
- Higher fidelity can tempt the project into SDK-surface expansion — that would violate the existing architecture direction and dilute the “EF Core InMemory for Azure-like testing” feel
- Public-vs-test-only helper boundaries may tighten further once richer fidelity is added — the project should preserve useful inspection surfaces without making the library feel like a raw state bag

## Existing Codebase / Prior Art

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — current truth surface for in-memory Service Bus topology, pending/completed/dead-lettered/errored observability, and the most likely anchor point for deeper M002 fidelity
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs` — current in-memory Service Bus sender/processor/admin behavior; this shows what M001 already supports and where richer behavior may need to be introduced carefully
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — proves Blob is intentionally basic today and helps explain why Blob Trigger is a separate later concern rather than an automatic M002 addition
- `src/Azure.InMemory/Azure.InMemory.csproj` — currently has minimal package metadata, confirming that packaging/polish work is still mostly ahead of the project
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusIngressTests.cs` — current proof for topology-aware queue/topic/subscription ingress and a baseline for selecting the next fidelity scenarios
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs` — current proof for processor execution and settlement; likely prior art for any retry/redelivery/delivery-count or related behavior work

> See `.gsd/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R020 — M002 is the provisional owner for advanced Service Bus fidelity; this milestone should turn at least the next most valuable behavior(s) into explicit planned, testable proof
- R022 — M002 is also the provisional owner for NuGet/publication readiness, but the clarified bar is **internal-ready only**, not public release pressure
- R021 — Azure Functions blob-trigger integration stays deferred and should not silently become required scope for M002

## Scope

### In Scope

- choosing and implementing the next most valuable Service Bus fidelity improvements after M001
- preserving or extending useful test observability where richer behavior would otherwise become opaque
- packaging, docs, examples, and polish needed to make Azure.InMemory feel solid for internal package consumption
- proving package consumption from a real consumer boundary rather than only from inside the producing project
- keeping the project lean and aligned with the established explicit provider seam

### Out of Scope / Non-Goals

- re-architecting the provider seam or abandoning the explicit resource-specific registration model
- chasing full Azure SDK parity or turning the library into a drop-in SDK replacement
- forcing public NuGet publication in M002
- Azure Functions Blob Trigger emulation in memory for this milestone
- broad host/runtime emulation beyond what is necessary for the chosen fidelity scenarios

## Technical Constraints

- Preserve the explicit resource-specific DI pattern such as `AddAzureServiceBusSdk()` / `AddAzureServiceBusInMemory()`
- Keep the project lean (`enxuto`) and reuse code where possible rather than widening the surface casually
- Supported M002 scenarios must still run inside `dotnet test` without Azure, Docker, or external emulator infrastructure
- Do not turn M002 into a re-architecture milestone; deepen fidelity within the seam established in M001
- Internal package readiness must be proven through real pack/install/use behavior, not only by adding metadata fields to the `.csproj`

## Integration Points

- `Microsoft.Extensions.DependencyInjection` composition seam — M002 must continue to plug into the existing explicit SDK-vs-in-memory registration model
- Service Bus consumer tests — richer fidelity must remain usable from real test code, not only internal state manipulation
- `.NET pack` / package consumption flow — packaging proof should validate that another internal consumer can restore and use the package successfully
- Existing in-memory state roots (`InMemoryServiceBusState`, `InMemoryBlobState`, `InMemoryKeyVaultState`) — observability patterns established in M001 are important prior art for M002

## Open Questions

- Which Service Bus behaviors are the first M002 fidelity targets: redelivery/retry/delivery count, lock/settlement edge cases, scheduling/deferred messages, or something else — current thinking: this is the main planning decision still left open, and the milestone should stay narrow instead of chasing many behaviors at once
- What exactly counts as **internal-ready only** packaging proof: local `.nupkg` consumption, internal feed readiness, docs/examples quality, CI pack artifacts, or a combination — current thinking: M002 should prove real internal consumption, but the exact bar should be sharpened during planning
- Which existing or reference consumer project should serve as the realism anchor for M002 acceptance — current thinking: using a close-in consumer will keep fidelity work grounded in real integration pain instead of abstract feature chasing
