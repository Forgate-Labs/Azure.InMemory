# S01 — Research

**Date:** 2026-03-30

## Summary

S01 owns R001-R004 and establishes the seam every later slice depends on. The main finding is that the repository is still greenfield: there is no solution, no project file, no C# source, and no existing DI pattern to extend. That means the planner should treat S01 as both architecture-seeding work and the first runnable vertical slice: create the .NET 10 solution/library/test skeleton, define the three public resource-specific registrations, and prove DI resolution with tests before any in-memory behavior is implemented.

The cleanest approach is to separate **Azure SDK client registration** from **provider seam selection**. Let callers register official SDK clients with `Microsoft.Extensions.Azure` / existing DI, then have `AddAzureServiceBusSdk()`, `AddAzureBlobSdk()`, and `AddAzureKeyVaultSdk()` bind the focused factory abstractions to those registered clients. The in-memory variants should own their singleton state roots and register the same factory abstractions. This keeps the public seam explicit, avoids re-implementing credential/configuration plumbing inside this library, and gives downstream slices stable in-memory state objects to build on.

## Recommendation

Create one public focused factory abstraction per resource and keep each contract scoped to the exact M001 behavior the downstream slices need, not the whole Azure SDK surface. For Service Bus, that means enough surface for sender creation, processor creation, and topology/admin access; for Blob, enough surface for container/blob write-read flow; for Key Vault, enough surface for `SetSecret` / `GetSecret`. The SDK backend should be thin adapters over registered Azure SDK clients; the in-memory backend should register singleton state roots plus parallel factory implementations that later slices extend.

Prefer this shape over returning raw Azure SDK clients directly from the public seam. Raw-client exposure would either force later in-memory slices to emulate more of the SDK than M001 needs or push business code back toward direct SDK coupling. A small set of library-owned abstractions keeps R002 credible and gives S02-S05 room to add truthful in-memory behavior without re-breaking S01.

## Implementation Landscape

### Key Files

- `README.md` — currently only contains the repository title; there is no existing architecture or project structure to preserve.
- `Azure.InMemory.sln` — first solution file to create; should reference the core library and the tests so `dotnet test` becomes the stable top-level verification entry point.
- `Directory.Build.props` — central place to lock `net10.0`, nullable, implicit usings, warnings, and any shared test/build defaults.
- `Directory.Packages.props` — optional but useful immediately because S01 will introduce multiple Azure SDK packages plus test packages; version pinning here keeps later slices smaller.
- `src/Azure.InMemory/Azure.InMemory.csproj` — core library; package refs should include at least `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Azure`, `Azure.Messaging.ServiceBus`, `Azure.Storage.Blobs`, and `Azure.Security.KeyVault.Secrets`.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — public Service Bus seam. Keep it minimal but include the capabilities S02/S03 will need so later slices do not have to break the contract.
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — public Blob seam for the basic S05 write/read path.
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — public Key Vault seam for the S04 `SetSecret` / `GetSecret` path.
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs` — `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()`; this is the explicit seam required by R001/R004.
- `src/Azure.InMemory/DependencyInjection/AzureBlobRegistrationExtensions.cs` — Blob equivalents.
- `src/Azure.InMemory/DependencyInjection/AzureKeyVaultRegistrationExtensions.cs` — Key Vault equivalents.
- `src/Azure.InMemory/ServiceBus/Sdk/AzureServiceBusSdkFactory.cs` — thin adapter over the registered `ServiceBusClient` (and, if needed, `ServiceBusAdministrationClient`).
- `src/Azure.InMemory/Blob/Sdk/AzureBlobSdkFactory.cs` — thin adapter over the registered `BlobServiceClient`.
- `src/Azure.InMemory/KeyVault/Sdk/AzureKeyVaultSdkFactory.cs` — thin adapter over the registered `SecretClient`.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs` — singleton state root registered by `AddAzureServiceBusInMemory()`; later slices should extend this instead of replacing it.
- `src/Azure.InMemory/Blob/InMemory/InMemoryBlobState.cs` — singleton state root for Blob.
- `src/Azure.InMemory/KeyVault/InMemory/InMemoryKeyVaultState.cs` — singleton state root for Key Vault.
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — the first test project; xUnit is a reasonable default because the repo is empty.
- `tests/Azure.InMemory.Tests/DependencyInjection/ProviderRegistrationTests.cs` — prove each resource can select SDK or in-memory mode and resolve the same public factory abstraction to the expected backend.

### Build Order

1. **Create the solution + library + test skeleton first.** Nothing exists yet, so establishing a buildable .NET 10 baseline is the first unblocker for every later slice.
2. **Define the public factory abstractions and resource-specific registration extensions next.** This retires the highest-risk ambiguity in R001/R002/R004 and gives downstream slices stable contracts.
3. **Implement SDK adapters that depend on DI-registered Azure SDK clients.** This proves the seam supports real clients without solving credentials/config inside the library.
4. **Implement in-memory registration plus singleton state roots.** Even if the state types are still thin in S01, they must exist now so S02-S05 can layer behavior onto them instead of reworking registrations.
5. **Add registration tests last.** Prove that the host can choose SDK or in-memory registration per resource and resolve the same public factory abstraction in either mode.

### Verification Approach

- `dotnet test` from the repository root should be the main proof for S01.
- Add tests that register each SDK mode and each in-memory mode independently, then assert:
  - the public factory abstraction resolves successfully from DI;
  - the resolved implementation type matches the chosen backend;
  - the in-memory backend exposes a shared singleton state root for later slices;
  - mixed-resource composition works (for example, Service Bus in-memory + Blob SDK + Key Vault in-memory in one service collection).
- Add one negative test for ambiguous registration if the implementation chooses fail-fast semantics when both SDK and in-memory registration methods are called for the same resource.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Azure SDK client registration and lifetime management | `Microsoft.Extensions.Azure` (`AddAzureClients`, named Azure clients, shared credential/config plumbing) | It already solves DI registration, named clients, and lifetime concerns; S01 should focus on the provider seam, not re-implement Azure client bootstrapping. |

## Constraints

- The repo is effectively empty: no `.sln`, no `.csproj`, no `.cs` files, and no existing namespaces or folder conventions to extend.
- Target framework is constrained to .NET 10.
- S01 owns R001-R004, so the public seam chosen here must survive S02-S05 without another contract reset.
- In-memory mode must stay runnable inside `dotnet test` with no Azure, Docker, or emulator dependencies.

## Common Pitfalls

- **Over-broad factory contracts** — if the public interfaces try to mirror the Azure SDK, later slices will spend their time emulating unused surface. Keep only the methods M001 actually needs.
- **Putting credential/config logic inside `AddAzure*Sdk()`** — this duplicates `Microsoft.Extensions.Azure` and makes tests harder. Let the library adapt already-registered Azure clients instead.
- **Forgetting stable singleton state roots for in-memory mode** — if S02-S05 have to replace registrations just to add behavior, S01 will have created the wrong seam.
- **Ambiguous same-resource registration** — decide early whether calling both `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()` is illegal (recommended: fail fast) or last-registration-wins; otherwise test behavior will be surprising.

## Open Risks

- The exact Service Bus factory shape is the highest-risk design choice: if topology/admin access is omitted now, S02 will have to widen the public contract.
- The repo name (`Azure.InMemory` folder) and current README title (`AzureResourcesInMemory`) do not yet tell you the intended product/package/namespace name; decide this once when creating the first `.sln` / `.csproj` so later slices do not churn paths and namespaces.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Azure Service Bus / Azure SDK for .NET | `sickn33/antigravity-awesome-skills@azure-servicebus-dotnet` | available |
| `Microsoft.Extensions.DependencyInjection` | none found via `npx skills find` | none found |

## Sources

- `ServiceBusClient` is intended to be cached and used as a singleton, and processors are created from it; that supports registering a client once and adapting it behind the focused factory seam. (source: [Azure Service Bus Topics Quickstart With .NET - Azure Service Bus | Microsoft Learn](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dotnet-how-to-use-topics-subscriptions))
- Azure SDK clients can be registered through `Microsoft.Extensions.Azure` using `AddAzureClients`, which supports the recommended split between SDK client registration and seam selection. (source: [Guide for running C# Azure Functions in the isolated worker model | Microsoft Learn](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide.md))
- `SecretClient` registration patterns in Microsoft docs also assume a DI-managed Azure client rather than library-owned credential bootstrapping. (source: [.NET Configuration Provider - Azure App Configuration | Microsoft Learn](https://learn.microsoft.com/en-us/azure/azure-app-configuration/reference-dotnet-provider))