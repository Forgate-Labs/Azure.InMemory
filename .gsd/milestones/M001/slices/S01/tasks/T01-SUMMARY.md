---
id: T01
parent: S01
milestone: M001
provides: []
requires: []
affects: []
key_files: ["Azure.InMemory.sln", "Directory.Build.props", "Directory.Packages.props", "src/Azure.InMemory/Azure.InMemory.csproj", "tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj", "src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs", "src/Azure.InMemory/Blob/IAzureBlobFactory.cs", "src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs", "tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs", "tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs", "tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs", "tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs"]
key_decisions: ["D005: keep the public seam resource-specific and expose small library-owned companion abstractions instead of returning raw Azure SDK clients"]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "`dotnet restore Azure.InMemory.sln` passed, `dotnet build Azure.InMemory.sln --no-restore` passed with a clean 0-warning/0-error build, and `dotnet test Azure.InMemory.sln --no-build` failed exactly at the intentionally seeded placeholder registration tests for Service Bus, Blob, Key Vault, and mixed composition."
completed_at: 2026-03-30T20:38:14.693Z
blocker_discovered: false
---

# T01: Bootstrapped the net10.0 Azure.InMemory solution with centralized props and focused resource factory contracts.

> Bootstrapped the net10.0 Azure.InMemory solution with centralized props and focused resource factory contracts.

## What Happened
---
id: T01
parent: S01
milestone: M001
key_files:
  - Azure.InMemory.sln
  - Directory.Build.props
  - Directory.Packages.props
  - src/Azure.InMemory/Azure.InMemory.csproj
  - tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj
  - src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs
  - src/Azure.InMemory/Blob/IAzureBlobFactory.cs
  - src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs
  - tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs
key_decisions:
  - D005: keep the public seam resource-specific and expose small library-owned companion abstractions instead of returning raw Azure SDK clients
duration: ""
verification_result: mixed
completed_at: 2026-03-30T20:38:14.696Z
blocker_discovered: false
---

# T01: Bootstrapped the net10.0 Azure.InMemory solution with centralized props and focused resource factory contracts.

**Bootstrapped the net10.0 Azure.InMemory solution with centralized props and focused resource factory contracts.**

## What Happened

Created the first buildable net10.0 solution for the repository, centralized shared build/package policy, established the stable Azure.InMemory assembly and namespace root, and defined focused public factory contracts for Service Bus, Blob, and Key Vault. The Service Bus seam includes small companion abstractions for sender, processor, and administration needs expected in later tasks, while Blob and Key Vault stay limited to the basic write/read and set/get flows planned for M001. I also created the four slice verification test files as explicit failing placeholders so the slice has a stable verification surface before DI registration behavior is implemented in T02-T05.

## Verification

`dotnet restore Azure.InMemory.sln` passed, `dotnet build Azure.InMemory.sln --no-restore` passed with a clean 0-warning/0-error build, and `dotnet test Azure.InMemory.sln --no-build` failed exactly at the intentionally seeded placeholder registration tests for Service Bus, Blob, Key Vault, and mixed composition.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet restore Azure.InMemory.sln` | 0 | ✅ pass | 1345ms |
| 2 | `dotnet build Azure.InMemory.sln --no-restore` | 0 | ✅ pass | 1850ms |
| 3 | `dotnet test Azure.InMemory.sln --no-build` | 1 | ❌ fail | 1801ms |


## Deviations

Created the four slice verification test files during T01 even though the task expected-output list focused on the solution and contract files, so the first task would seed the slice’s verification surface.

## Known Issues

`dotnet test Azure.InMemory.sln --no-build` currently fails because provider registration and mixed-composition behavior are not implemented yet; the four registration test files are intentional placeholders for T02-T05.

## Files Created/Modified

- `Azure.InMemory.sln`
- `Directory.Build.props`
- `Directory.Packages.props`
- `src/Azure.InMemory/Azure.InMemory.csproj`
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj`
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
- `src/Azure.InMemory/Blob/IAzureBlobFactory.cs`
- `src/Azure.InMemory/KeyVault/IAzureKeyVaultFactory.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/ServiceBusProviderRegistrationTests.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/BlobProviderRegistrationTests.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/KeyVaultProviderRegistrationTests.cs`
- `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs`


## Deviations
Created the four slice verification test files during T01 even though the task expected-output list focused on the solution and contract files, so the first task would seed the slice’s verification surface.

## Known Issues
`dotnet test Azure.InMemory.sln --no-build` currently fails because provider registration and mixed-composition behavior are not implemented yet; the four registration test files are intentional placeholders for T02-T05.
