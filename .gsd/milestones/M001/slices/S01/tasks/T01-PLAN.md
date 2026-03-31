---
estimated_steps: 4
estimated_files: 8
skills_used: []
---

# T01: Bootstrap the .NET 10 solution and focused factory contracts

**Slice:** S01 — Provider registration and focused factories
**Milestone:** M001

## Description

Create the first buildable `net10.0` solution for the repository and define the three library-owned resource factory contracts that all later slices will extend. This task seeds the solution, shared package/build settings, and stable `Azure.InMemory` naming so the rest of the milestone can add registration and behavior without path or namespace churn.

## Steps

1. Create `Azure.InMemory.sln`, `Directory.Build.props`, and `Directory.Packages.props` to centralize the target framework, nullable/implicit usings, warnings, and package versions.
2. Add `src/Azure.InMemory/Azure.InMemory.csproj` and `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj`, wire the project reference, and include the Azure SDK / DI / xUnit dependencies needed for the later registration work.
3. Define `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` as focused, library-owned seams sized for M001 scenarios rather than wrappers over the full SDK surface.
4. Keep the Service Bus contract future-proof for S02/S03 sender, processor, and topology/admin needs while keeping Blob and Key Vault contracts sized only for the basic write/read flows planned in S05 and S04.

## Must-Haves

- [ ] `Azure.InMemory.sln` builds with `net10.0` library and test projects.
- [ ] Central props files pin shared build settings and package versions instead of repeating them per project.
- [ ] The root solution / assembly / namespace naming is `Azure.InMemory`.
- [ ] All three public factory contracts compile and stay focused on the M001 resource scenarios.

## Verification

- `dotnet restore Azure.InMemory.sln`
- `dotnet build Azure.InMemory.sln`

## Inputs

- `README.md` — current repository identity to align solution naming
- `.gsd/REQUIREMENTS.md` — R001-R004 coverage to preserve in the contracts
- `.gsd/DECISIONS.md` — existing architectural decisions, especially D002
- `.gsd/milestones/M001/slices/S01/S01-RESEARCH.md` — recommended project layout and contract scope

## Expected Output

- `Azure.InMemory.sln` — first solution entry point for the milestone
- `Directory.Build.props` — shared target framework and compiler policy
- `Directory.Packages.props` — centralized NuGet version pinning
- `src/Azure.InMemory/Azure.InMemory.csproj` — core library project
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj` — xUnit test project
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs` — focused Service Bus seam
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs` — focused Blob seam
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs` — focused Key Vault seam
