# S03: External consumer package proof — Research

**Date:** 2026-03-31
**Status:** Ready for planning

## Summary

S03 is a **targeted research** slice. It primarily owns the final external-consumer proof for **R022** (internal-ready package consumption), and it supports **R020** indirectly by making the consumer proof exercise the packaged Service Bus behavior that M002 already deepened in S01.

The repo is already in a strong producer state for this slice:

- `./Azure.InMemory.sln` exists and only contains the producer library plus the repo-internal test project.
- `src/Azure.InMemory/Azure.InMemory.csproj` now has intentional package metadata and a baseline package version of `1.0.0`.
- `README.md` is already package-facing and documents the real public Service Bus seam.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg` plus `artifacts/pack/package-inspection.txt` already exist.

The biggest S03 surprise was **not** in the package itself; it was in the repo root build configuration. A fresh consumer project created **under this repo root** automatically inherits `Directory.Packages.props`, which enables Central Package Management. In a direct probe, a standalone xUnit consumer project inside `artifacts/.tmp-s03-probe/` failed restore with `NU1008` until I explicitly set:

```xml
<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
```

After that opt-out, the probe restored `Azure.InMemory` from the local folder feed at `./artifacts/pack` and passed a meaningful package-only redelivery test with:

- **no `ProjectReference`** to the producer project,
- a local `NuGet.Config` that pointed to `./artifacts/pack` plus `nuget.org`,
- an isolated packages folder during restore (`--packages ./<consumer>/.nuget/packages --no-cache`),
- a queue scenario that used `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, explicit topology, sender, processor, and two `StartProcessingAsync()` runs to prove deterministic redelivery.

That probe is the strongest planning signal for S03: the package/use path is already viable, and the main implementation work is turning that into durable, committed proof with the right repo boundary and verification discipline.

## Requirement Posture

### Requirement this slice owns

- **R022 — internal-ready package consumption proof.**
  S02 advanced the producer boundary (metadata, README, emitted `.nupkg`). S03 owns the final “another project can really restore and use it” acceptance step.

### Requirement this slice supports

- **R020 — observable Service Bus fidelity.**
  S03 does not add new Service Bus behavior, but the external consumer proof should use the S01 redelivery contract so the package consumer validates a meaningful M002 behavior rather than a trivial send-only smoke test.

## What I Verified Directly In This Worktree

### 1) Current producer tree and solution boundary

`Azure.InMemory.sln` currently contains only:

- `src/Azure.InMemory/Azure.InMemory.csproj`
- `tests/Azure.InMemory.Tests/Azure.InMemory.Tests.csproj`

There is **no existing standalone consumer/sample project** in the repo. S03 will need to introduce one if the proof is meant to be durable and repeatable from source control.

### 2) Producer package shape

`src/Azure.InMemory/Azure.InMemory.csproj` currently declares:

- `PackageId`: `Azure.InMemory`
- `Version`: `1.0.0`
- `PackageReadmeFile`: `README.md`
- package metadata already aligned for internal-ready consumption

The packed artifact inspection in `artifacts/pack/package-inspection.txt` confirms the `.nupkg` contains:

- `Azure.InMemory.nuspec`
- `lib/net10.0/Azure.InMemory.dll`
- `README.md`

That means the current package proof target is **net10.0-only**. Any external consumer proof inside this milestone should target `net10.0` rather than trying to broaden compatibility.

### 3) Public API seams the consumer should actually use

The package-facing Service Bus seam is already stable and documented:

- `Azure.InMemory.DependencyInjection.AddAzureServiceBusInMemory()`
- `Azure.InMemory.ServiceBus.IAzureServiceBusFactory`
- `factory.Administration.CreateQueueAsync(...)`
- `factory.CreateSender(...)`
- `factory.CreateQueueProcessor(...)`
- `AzureServiceBusProcessorOptions`
- `AzureServiceBusReceivedMessageContext.CompleteMessageAsync(...)`
- `AzureServiceBusReceivedMessageContext.DeliveryCount`

The in-memory inspection surface is also public and usable from a package consumer:

- `Azure.InMemory.ServiceBus.InMemory.InMemoryServiceBusState`

Important constraint already captured in repo knowledge and reflected in the README:

- `InMemoryServiceBusState.GetSubscriptionEntityPath(...)` is **internal**, so any external consumer that asserts subscription outcomes must use the literal canonical string:
  - `<topic>/Subscriptions/<subscription>`

### 4) README quality for the consumer scenario

`README.md` already gives the exact public-API path S03 should follow:

- install package
- call `AddAzureServiceBusInMemory()`
- resolve `IAzureServiceBusFactory`
- explicitly declare topology before send/process
- use `StartProcessingAsync()` for deterministic processor runs
- inspect state through `InMemoryServiceBusState`
- understand that retries happen only on the **next explicit** `StartProcessingAsync()` call

This is a strong signal that S03 probably **does not need public API changes**. It is mainly a consumer-proof/build-verification slice.

## Probe Findings That Matter For Planning

I ran a direct external-consumer probe inside this worktree.

### Probe result 1 — naïve in-repo consumer restore fails because of CPM inheritance

A standalone probe project under `artifacts/.tmp-s03-probe/` initially failed restore with:

- `NU1008: The following PackageReference items cannot define a value for Version ... Projects using Central Package Management must define a Version value on a PackageVersion item.`

Root cause:

- repo-root `Directory.Packages.props` applies to child projects even when they are **not** in the solution.

Implication for S03:

- If the committed consumer project lives anywhere under this repo root, it must either:
  1. set `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` in its own `.csproj`, or
  2. live outside this repo tree entirely.

For a committed proof project inside the repo, option **(1)** is the cleanest.

### Probe result 2 — package-only restore/test succeeds once CPM is disabled

After adding:

```xml
<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
```

restore succeeded against a local `NuGet.Config` that declared:

- local folder feed = `$(repo)/artifacts/pack`
- `nuget.org`

Then `dotnet test --no-restore` passed a meaningful xUnit test that:

- referenced `Azure.InMemory` via `PackageReference`,
- used `AddAzureServiceBusInMemory()` and `IAzureServiceBusFactory`,
- created queue topology explicitly,
- sent a `ServiceBusMessage`,
- configured `MaxDeliveryCount: 2`,
- failed the first handler invocation,
- asserted a pending message with `DeliveryCount == 2`,
- reran `StartProcessingAsync()`,
- asserted a completed message with `DeliveryCount == 2`.

This confirms the package is already consumable through the local folder feed and that the README path is executable from outside the producer project reference boundary.

### Probe result 3 — the package transitives are sufficient for the README scenario

The probe project **did not** need direct package references for:

- `Microsoft.Extensions.DependencyInjection`
- `Azure.Messaging.ServiceBus`

It compiled with only:

- `Azure.InMemory`
- xUnit/test SDK packages

That means the current `Azure.InMemory` package already carries the compile-time transitive dependencies needed for the README quickstart and an xUnit-based consumer proof.

## Implementation Landscape

## Existing files that matter

- `Azure.InMemory.sln`
  - only producer library + internal tests today; no consumer proof project exists yet.
- `Directory.Build.props`
  - repo-wide defaults (`net10.0`, nullable, analyzers, warnings-as-errors) that will flow into any child consumer project.
- `Directory.Packages.props`
  - repo-wide Central Package Management; this is the main hidden constraint for an in-repo consumer project.
- `src/Azure.InMemory/Azure.InMemory.csproj`
  - authoritative package identity/version/readme settings.
- `README.md`
  - authoritative package-facing quickstart; S03 should follow this, not invent a different consumer story.
- `artifacts/pack/Azure.InMemory.1.0.0.nupkg`
  - current producer artifact for local-feed restore.
- `artifacts/pack/package-inspection.txt`
  - durable producer-boundary proof from S02.
- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs`
  - proves `AddAzureServiceBusInMemory()` is the correct consumer registration entry point.
- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
  - defines the public sender/processor/admin surface the external consumer should use.
- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
  - public inspection surface; subscription helper remains internal, so consumer assertions must use the literal canonical path string.
- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
  - best source pattern for a meaningful S03 consumer scenario; queue redelivery is the cleanest package-proof case.

## Natural seams for planning

### Seam A — consumer harness scaffolding

This is independent from producer code changes.

Likely files to create:

- standalone consumer `.csproj`
- `NuGet.Config`
- one or more consumer proof tests

Recommended boundary:

- keep the consumer project **outside** `Azure.InMemory.sln`
- keep it **package-only** (no `ProjectReference`)
- if it lives under this repo root, include `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`

### Seam B — proof scenario selection

The lowest-risk meaningful scenario is the **queue redelivery** path, because it is:

- already proven in repo tests,
- already documented in README semantics,
- deterministic under two explicit `StartProcessingAsync()` calls,
- strong enough to show that the consumer is using the real package, not just compiling a trivial registration smoke test.

A subscription-path scenario is possible, but it adds the canonical entity-path assertion complexity and is not necessary unless the slice wants stronger topic/subscription realism.

### Seam C — package/feed isolation

S03 must make stale-cache false confidence impossible or at least unlikely.

Viable strategies:

1. **Safest:** pack with an intentional unique S03 version override and consume that version.
2. **Also workable:** keep `1.0.0`, but restore with `--no-cache` into a dedicated `--packages` directory under the consumer folder.

Because D039 explicitly allows downstream version override when needed, option **(1)** is the cleanest if S03 repacks. My probe proves option **(2)** also works with the current baseline artifact.

## Recommendation

Plan S03 as three small tasks in order:

### 1) Establish a committed standalone consumer proof harness

Build a durable consumer project that is clearly outside the producer project boundary.

Key constraints:

- no `ProjectReference`
- not included in `Azure.InMemory.sln` unless there is a compelling reason
- explicit local-feed `NuGet.Config`
- explicit opt-out from root CPM if the consumer lives under this repo tree

### 2) Implement one meaningful package-consumption test using only public package APIs

Recommended test shape:

- xUnit consumer test
- `ServiceCollection`
- `AddAzureServiceBusInMemory()`
- resolve `IAzureServiceBusFactory`
- create queue topology
- send one message
- fail first handler invocation
- assert pending retry state via `InMemoryServiceBusState`
- rerun processor
- assert completed outcome

This gives S03 a real consumer success path while still exercising the M002 fidelity improvement from S01.

### 3) Verify the real producer→package→consumer loop end-to-end

Do not rely on prior local caches.

At minimum:

- produce or reuse a known package artifact
- restore consumer through a local folder feed
- run consumer `dotnet test`
- keep the producer solution green separately

## Verification Strategy

## Commands I would expect the executor to run

### Producer regression / package artifact

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test ./Azure.InMemory.sln
DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack
```

If S03 wants a cache-resistant unique version:

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack -p:Version=1.0.0-s03.1
```

### Consumer restore / test

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet restore ./<consumer>/<ConsumerProject>.csproj \
  --configfile ./<consumer>/NuGet.Config \
  --packages ./<consumer>/.nuget/packages \
  --no-cache

DOTNET_CLI_UI_LANGUAGE=en dotnet test ./<consumer>/<ConsumerProject>.csproj --no-restore
```

### Structural checks worth adding

- assert the consumer `.csproj` contains `PackageReference Include="Azure.InMemory" ...`
- assert the consumer `.csproj` contains **no** `ProjectReference`
- if the consumer lives under repo root, assert it contains `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`
- if the scenario proves subscription inspection, assert it uses the literal canonical string `<topic>/Subscriptions/<subscription>`

## Constraints / Risks

### 1) Repo-root Central Package Management will surprise any in-repo consumer project

This is the most important non-obvious planning risk.

If the consumer project is committed anywhere under this repo root, restore will inherit `Directory.Packages.props` unless explicitly disabled.

### 2) The package currently only ships `lib/net10.0`

S03 should target `net10.0`. Broader TFM work would be a separate concern and is not required to prove internal-ready package consumption in this milestone.

### 3) Cache management matters because the baseline package version is fixed at `1.0.0`

If S03 republishes the same version after producer changes, stale restore state can fake success. Use either:

- a unique version override, or
- isolated packages folder + `--no-cache`.

### 4) The proof should stay on the package-facing seam, not repo internals

The consumer test can use `InMemoryServiceBusState` because README already documents it as a test-only inspection surface, but it should avoid repo-only helpers or project references. In particular, do not depend on `GetSubscriptionEntityPath(...)` because that helper is internal.

## Skill Notes

## Installed skill guidance that materially applies

The loaded **`error-handling-patterns`** skill reinforces three rules that fit S03 exactly:

- **Fail Fast** — restore/setup issues should surface directly instead of being hidden behind helper scripts.
- **Meaningful Messages** — the proof should preserve actionable output from `dotnet restore` / `dotnet test`, and it should lean on the library’s existing actionable `InvalidOperationException` messages when topology is wrong.
- **Don’t Swallow Errors** — the consumer proof should make restore/test failures first-class evidence, not normalize them away.

That matches the direct-command verification style already used in earlier slices.

## Skill discovery suggestions

No installed skill is a perfect match for local NuGet consumer proof, but these search results were the best direct matches for the core technologies in S03:

- **NuGet / package management**
  - `github/awesome-copilot@nuget-manager` — **8.2K installs**
  - Install: `npx skills add github/awesome-copilot@nuget-manager`
  - Why promising: strongest result for NuGet/package-feed workflow issues.

- **Azure Service Bus (.NET)**
  - `sickn33/antigravity-awesome-skills@azure-servicebus-dotnet` — **44 installs**
  - Install: `npx skills add sickn33/antigravity-awesome-skills@azure-servicebus-dotnet`
  - Why promising: directly aligned with the consumer proof scenario the package test should exercise.

- **.NET testing / xUnit project setup**
  - `novotnyllc/dotnet-artisan@dotnet-testing` — **52 installs**
  - Install: `npx skills add novotnyllc/dotnet-artisan@dotnet-testing`
  - Why promising: best high-signal general testing result for a standalone consumer proof project.
  - Alternative: `npx skills add kevintsengtw/dotnet-testing-agent-skills@dotnet-testing-xunit-project-setup` (**43 installs**) if the main pain becomes xUnit project scaffolding rather than package flow.

## Bottom Line For The Planner

S03 does **not** look like a public API slice. It looks like a **proof harness** slice.

The shortest path is:

1. create a standalone package-only consumer project,
2. make it independent from repo-root CPM,
3. wire a local folder feed to `./artifacts/pack`,
4. prove one real queue redelivery flow through `AddAzureServiceBusInMemory()` and `IAzureServiceBusFactory`,
5. verify restore/test in a cache-resistant way.

The direct probe already proves the package can do this today. The planner’s job is mostly to turn that successful probe into committed, repeatable evidence with the right repo boundary and verification commands.
