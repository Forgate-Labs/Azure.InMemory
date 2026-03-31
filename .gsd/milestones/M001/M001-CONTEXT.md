# M001: Core in-memory Azure providers

**Gathered:** 2026-03-30
**Status:** Ready for planning

## Project Description

Build a .NET test library that behaves like an Azure-focused version of EF Core InMemory. Test projects choose explicit resource registrations such as `AddAzureServiceBusSdk()` or `AddAzureServiceBusInMemory()` and consume focused factories instead of talking straight to the Azure SDK clients everywhere. The first milestone delivers a useful MVP for Service Bus, Blob, and Key Vault so projects can run meaningful integration-style tests with `dotnet test` and no external infrastructure.

## Why This Milestone

This milestone establishes the seam the whole project depends on: resource-specific registration plus focused factories with real-SDK and in-memory backends. It also proves the library is not just a collection of mocks by delivering one meaningful end-to-end in-memory Service Bus processor flow, alongside basic Blob and Key Vault operations. Without this milestone, later fidelity work has nowhere stable to land.

## User-Visible Outcome

### When this milestone is complete, the user can:

- register either official Azure SDK-backed providers or in-memory providers for Service Bus, Blob, and Key Vault in a test host
- run `dotnet test` and execute a real in-memory Service Bus processor flow where a message is published, consumed, and either completed or dead-lettered with observable results

### Entry point / environment

- Entry point: test host composition via `IServiceCollection` extensions and focused factories
- Environment: local dev / CI test process
- Live dependencies involved: none for in-memory mode; Azure SDK packages remain integration points for SDK mode

## Completion Class

- Contract complete means: provider registrations, focused factories, and the supported in-memory resource behaviors are covered by tests and exposed through stable APIs
- Integration complete means: the same composition seam supports both SDK and in-memory backends, and the Service Bus processor path works end-to-end in memory
- Operational complete means: provider instances and in-memory state can be created, used, and torn down cleanly inside a test run without external services

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- a test host can swap between `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()` without changing the business logic contract that depends on the focused Service Bus factory
- an in-memory Service Bus topic/subscription scenario can publish a message, run a processor, and leave an observable completed or dead-lettered outcome for test assertions
- Blob and Key Vault basic read/write scenarios run entirely in process under `dotnet test`; Docker, Azure resources, and other external emulators are not required

## Risks and Unknowns

- Service Bus processor lifecycle and settlement semantics may be more complex than they look — if this seam is wrong, downstream test ergonomics and fidelity both suffer
- Focused factories may still drift into a mini-framework if the contracts are too broad — that would make the library harder to adopt and maintain
- Extra inspection APIs for tests could leak too much implementation detail — they need to be useful without making the public design messy

## Existing Codebase / Prior Art

- `README.md` — current repository is effectively empty, so M001 is establishing the first real project structure and conventions
- `EF Core InMemory` — the user's intended mental model for provider switching and test-time substitution
- `Azure.Messaging.ServiceBus` — prior art for processor creation, message settlement, and administration behaviors the in-memory design needs to echo where useful
- `Microsoft.Extensions.Azure` — prior art for Azure client registration, though not sufficient by itself for the provider seam this project needs

> See `.gsd/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R001 — establish resource-specific provider registration
- R002 — establish focused factories per Azure resource
- R003 — keep the full M001 loop runnable inside `dotnet test`
- R004 — make the seam capable of both SDK and in-memory backends
- R005 — prove the in-memory Service Bus publish/receive path
- R006 — support complete and dead-letter settlement in the processor path
- R007 — expose failure visibility and harness state for tests
- R008 — provide in-memory Key Vault read/write
- R009 — provide in-memory Blob read/write
- R010 — expose test-only inspection APIs beyond the official SDK surface

## Scope

### In Scope

- resource-specific DI extensions for SDK and in-memory modes
- focused factories for Service Bus, Blob, and Key Vault
- basic in-memory Service Bus topology creation and message ingress
- in-memory Service Bus processor execution with complete and dead-letter outcomes
- test harness and inspection surfaces for supported in-memory scenarios
- basic in-memory Key Vault `SetSecret` and `GetSecret`
- basic in-memory Blob write and read behavior

### Out of Scope / Non-Goals

- zero-refactor consumer adoption
- full Azure SDK surface parity
- advanced Service Bus semantics such as sessions, scheduled messages, or full retry fidelity
- Azure Functions Blob Trigger support
- NuGet publication in this milestone

## Technical Constraints

- Use .NET 10
- Keep the project enxuto and reuse code wherever possible
- The public composition pattern stays explicit and resource-scoped, even if verbose
- In-memory mode must run without Azure, Docker, or any other external infrastructure
- The design may use extra APIs for tests beyond the official SDK surface, but should avoid turning into a broad Azure clone

## Integration Points

- Azure SDK packages — SDK-backed providers wrap the official clients behind the same focused factory seam
- `Microsoft.Extensions.DependencyInjection` — DI registration is the main entry point for switching between SDK and in-memory behavior
- test frameworks and `dotnet test` — the in-memory providers must be safe and ergonomic inside normal .NET test execution

## Open Questions

- exact shape of the focused factory contracts — current direction is one factory per resource, but the method set still needs to be pared to the minimum useful surface
- shape of the test harness APIs — they need to support strong assertions without becoming the primary programming model
- exact M002 scope split between fidelity work and packaging — we have the direction, but the detailed second milestone plan is intentionally deferred
