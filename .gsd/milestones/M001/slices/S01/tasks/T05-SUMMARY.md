---
id: T05
parent: S01
milestone: M001
provides: []
requires: []
affects: []
key_files: ["tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Kept the three registration extension implementations unchanged after inspection because their conflict guards and missing-client diagnostics were already consistent across Service Bus, Blob, and Key Vault."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests` and `dotnet test ./Azure.InMemory.sln`; both passed locally and provided the final infrastructure-free proof that mixed-resource composition and resource-specific conflict handling hold for S01."
completed_at: 2026-03-30T21:08:00.922Z
blocker_discovered: false
---

# T05: Replaced the mixed-provider placeholder with real cross-resource DI composition tests and closed S01 with a green full suite.

> Replaced the mixed-provider placeholder with real cross-resource DI composition tests and closed S01 with a green full suite.

## What Happened
---
id: T05
parent: S01
milestone: M001
key_files:
  - tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Kept the three registration extension implementations unchanged after inspection because their conflict guards and missing-client diagnostics were already consistent across Service Bus, Blob, and Key Vault.
duration: ""
verification_result: passed
completed_at: 2026-03-30T21:08:00.924Z
blocker_discovered: false
---

# T05: Replaced the mixed-provider placeholder with real cross-resource DI composition tests and closed S01 with a green full suite.

**Replaced the mixed-provider placeholder with real cross-resource DI composition tests and closed S01 with a green full suite.**

## What Happened

Replaced the placeholder MixedProviderCompositionTests failure with real integration-style DI assertions that compose Service Bus, Blob, and Key Vault registrations in one IServiceCollection. The new suite proves two mixed backend combinations so each resource is exercised in both SDK and in-memory modes across the final slice proof, and it adds resource-specific conflict checks that still fail after the other two resources have chosen different modes. I inspected the three registration extension implementations during execution and left them unchanged because the existing conflict guards and missing-client diagnostics were already aligned across resources. I also recorded a worktree-specific verification note in .gsd/KNOWLEDGE.md about avoiding parallel dotnet test runs against the same solution output paths when collecting final evidence.

## Verification

Ran `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests` and `dotnet test ./Azure.InMemory.sln`; both passed locally and provided the final infrastructure-free proof that mixed-resource composition and resource-specific conflict handling hold for S01.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests` | 0 | ✅ pass | 3703ms |
| 2 | `dotnet test ./Azure.InMemory.sln` | 0 | ✅ pass | 3659ms |


## Deviations

Used `./Azure.InMemory.sln` instead of the bare `Azure.InMemory.sln` spelling for shell verification because the explicit relative path resolved reliably in this worktree.

## Known Issues

None.

## Files Created/Modified

- `tests/Azure.InMemory.Tests/DependencyInjection/MixedProviderCompositionTests.cs`
- `.gsd/KNOWLEDGE.md`


## Deviations
Used `./Azure.InMemory.sln` instead of the bare `Azure.InMemory.sln` spelling for shell verification because the explicit relative path resolved reliably in this worktree.

## Known Issues
None.
