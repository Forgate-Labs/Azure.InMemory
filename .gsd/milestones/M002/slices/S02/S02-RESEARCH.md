# S02: Internal-ready package surface and docs — Research

**Date:** 2026-03-30
**Research depth:** Targeted

## Summary

There are **no active requirements** right now, so S02 is not retiring an active contract. It primarily advances **deferred R022 (NuGet publication readiness)**, interpreted by D034 as **internal-ready** rather than public-release hardening. It must also preserve the already-validated architecture and local-test loop from **R001/R002/R003** and the Service Bus behavior clarified in S01.

The current package is already technically consumable, but the package-facing surface is skeletal:

- `src/Azure.InMemory/Azure.InMemory.csproj` contains only `AssemblyName`, `RootNamespace`, and `Description`.
- root `README.md` is a one-line placeholder (`# AzureResourcesInMemory`).
- there is no existing docs/examples folder, no package readme, and no sample consumer project checked in.
- `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack` succeeds and emits `Azure.InMemory.1.0.0.nupkg`, but pack warns: **the package is missing a readme**.

I also verified that a minimal consumer can build and run against the packed artifact through a local folder source. The public seam is already good enough for docs/examples: the consumer only needs the `Azure.InMemory` package plus `using Azure.InMemory.DependencyInjection;` and `using Azure.InMemory.ServiceBus;` for the basic registration flow; `using Azure.InMemory.ServiceBus.InMemory;` is only needed when docs also show the test-only observability surface.

That means S02 should stay narrow and practical:
1. improve the `.csproj` package metadata/readme inclusion,
2. turn `README.md` into the authoritative package-facing quickstart,
3. prove `dotnet pack` remains clean/useful,
4. stop short of the full fresh-consumer proof, which belongs to S03.

## Requirement Focus

### Active requirements
- None.

### Deferred requirement advanced by this slice
- **R022 — NuGet publication readiness**
  - For M002 this means **internal-ready package surface and docs**, not public NuGet distribution.

### Validated constraints this slice must preserve
- **R001 / R002** — keep the existing explicit resource-specific registration and focused factory seam.
- **R003** — everything must still work from the normal `dotnet test` / `dotnet pack` loop with no external infrastructure.
- **D034** — internal-ready proof means real package artifact + package-facing docs/examples, not metadata theater.
- **S01 forward intelligence** — docs must preserve the newly-proven explicit processor rerun semantics and canonical subscription-path rules; they must not imply background retries or raw Azure SDK parity.

## Recommendation

Treat S02 as three tightly-scoped work units:

1. **Package metadata + package readme inclusion**
   - Work in `src/Azure.InMemory/Azure.InMemory.csproj`.
   - Add the minimum metadata that makes the `.nupkg` feel intentional for internal consumers: package readme, repository/project URL, license metadata, authors/company, tags, and an explicit versioning strategy.
   - Reuse existing repo assets rather than inventing new packaging-only copies where possible.

2. **One authoritative package-facing guide**
   - Expand root `README.md` into the internal-consumer quickstart.
   - Make it package-safe if it will be packed as the package readme: avoid fragile relative links or repo-only assumptions.
   - Put the minimal Service Bus path first, because the roadmap and S03 acceptance depend on `AddAzureServiceBusInMemory()` and a meaningful local messaging flow.

3. **Pack verification, not full external-consumer proof**
   - S02 should prove that `dotnet pack` emits a coherent package and that the docs point to the real public seam.
   - Leave the fresh consumer restore/use/test acceptance to S03.

I would **not** add a committed sample project in S02 unless the planner finds the README snippet alone is insufficient. There is no existing `samples/` or `examples/` infrastructure in the repo, so a checked-in sample would add more surface area than the slice currently needs. A strong README/package-readme example is the lowest-friction path.

## Implementation Landscape

### Files that matter

- `src/Azure.InMemory/Azure.InMemory.csproj`
  - Current packaging control point.
  - Today it has only:
    - `AssemblyName`
    - `RootNamespace`
    - `Description`
  - No explicit package readme, package/project URL, license metadata, tags, version, or other package-facing polish.

- `README.md`
  - Currently just `# AzureResourcesInMemory`.
  - This is the biggest concrete docs gap.
  - Best candidate for the package-facing quickstart if S02 wants one authoritative source.

- `LICENSE`
  - MIT license already exists at repo root.
  - This makes license metadata a low-risk packaging task rather than a content-discovery task.

- `Directory.Packages.props`
  - The repo uses **Central Package Management**.
  - This matters operationally for any scratch consumer probe created inside the repo tree.

- `src/Azure.InMemory/DependencyInjection/AzureServiceBusRegistrationExtensions.cs`
  - Defines the actual registration method docs must teach: `AddAzureServiceBusInMemory()`.
  - The extension lives in namespace `Azure.InMemory.DependencyInjection`, so docs must show that import explicitly.

- `src/Azure.InMemory/ServiceBus/IAzureServiceBusFactory.cs`
  - Defines the public Service Bus seam docs should describe:
    - `IAzureServiceBusFactory`
    - `IAzureServiceBusAdministration`
    - `IAzureServiceBusProcessor`
    - `AzureServiceBusProcessorOptions`
    - `AzureServiceBusReceivedMessageContext`
  - This is the package-facing contract, not the internal state implementation.

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusFactory.cs`
  - Documents the non-obvious runtime rules S02 must surface accurately:
    - queues/subscriptions must be declared before processing,
    - default `AutoCompleteMessages` is `false`,
    - successful but unsettled messages are requeued/preserved rather than implicitly completed,
    - failed deliveries requeue only on the next explicit `StartProcessingAsync()` run.

- `src/Azure.InMemory/ServiceBus/InMemory/InMemoryServiceBusState.cs`
  - Holds the inspectable observability surface that package docs can mention as a test-only differentiator:
    - `GetPendingMessages(...)`
    - `GetCompletedMessages(...)`
    - `GetDeadLetteredMessages(...)`
    - `GetErroredMessages(...)`
  - Also exposes the canonical subscription entity-path rule: `<topic>/Subscriptions/<subscription>`.

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusProcessorTests.cs`
  - Best current source for docs truth on:
    - explicit completion,
    - default non-autocomplete behavior,
    - topology declaration requirements.

- `tests/Azure.InMemory.Tests/ServiceBus/InMemory/InMemoryServiceBusRedeliveryTests.cs`
  - Best current source for docs truth on S01 behavior:
    - delivery-count progression,
    - explicit rerun redelivery,
    - max-delivery dead-letter semantics.

### Natural seams for planning

1. **Metadata seam** — `src/Azure.InMemory/Azure.InMemory.csproj`
   - isolated, low-risk, no runtime behavior change.

2. **Docs seam** — `README.md` (and only a second docs file if strictly necessary)
   - should be authored against the already-proven public API.

3. **Verification seam** — pack/build/test commands
   - should verify package coherence without dragging S02 into the full external-consumer acceptance that belongs to S03.

### What the package/docs need to teach explicitly

These are the important consumer-facing rules that are easy to miss if the docs stay vague:

- Import `Azure.InMemory.DependencyInjection` before calling `AddAzureServiceBusInMemory()`.
- Resolve `IAzureServiceBusFactory` from DI; do not imply consumers talk directly to raw Azure SDK clients.
- Declare topology before use:
  - `CreateQueueAsync(...)`
  - `CreateTopicAsync(...)`
  - `CreateSubscriptionAsync(...)`
- If using a processor with default options, call `CompleteMessageAsync(...)` explicitly or set `AutoCompleteMessages: true`; otherwise a “successful” handler that never settles leaves the message pending.
- The in-memory provider is intentionally deterministic and explicit:
  - retries/redelivery happen on the **next** explicit `StartProcessingAsync()` call,
  - not via background polling.
- For topic subscriptions, inspection and processing are anchored to canonical entity paths like `orders/Subscriptions/billing`.
- `InMemoryServiceBusState` is a **test-only inspection surface**, not the primary operational seam.

### Packaging baseline verified during research

I directly confirmed the following from this worktree:

- `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`
  - **passes**
  - emits `Azure.InMemory.1.0.0.nupkg`
  - warns that the package is missing a readme

- the emitted `.nupkg` currently contains the library and dependencies metadata, but the generated nuspec is still sparse:
  - `id: Azure.InMemory`
  - `version: 1.0.0` (defaulted)
  - `authors: Azure.InMemory` (defaulted)
  - description present
  - repository commit present
  - **no package readme**
  - **no explicit repository URL** in metadata
  - **no explicit license metadata**

- a manual scratch consumer project, restored against the local `./artifacts/pack` folder source, can:
  - reference `Azure.InMemory` as a package,
  - compile a minimal `ServiceCollection` + `AddAzureServiceBusInMemory()` program,
  - resolve `IAzureServiceBusFactory`,
  - resolve `InMemoryServiceBusState`,
  - create a queue successfully.

One more useful discovery: the consumer probe **did not** need a separate direct package reference to `Microsoft.Extensions.DependencyInjection`; the transitive assets from `Azure.InMemory` were enough for the minimal compile scenario.

## Verification

### Baseline commands already confirmed

- `dotnet test ./Azure.InMemory.sln`
  - **passes** (`74/74`)
- `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`
  - **passes**
  - currently warns about missing package readme

### Recommended S02 verification contract

Use these as the slice-level proof points:

1. `dotnet test ./Azure.InMemory.sln`
   - ensures package/docs changes did not disturb the validated runtime seam.

2. `dotnet pack ./src/Azure.InMemory/Azure.InMemory.csproj -c Release -o ./artifacts/pack`
   - should emit the package cleanly,
   - ideally without the current missing-readme warning,
   - and with the expected package-facing metadata/readme content included.

3. Optional lightweight package smoke check (only if planner wants a non-S03 guardrail)
   - build a temporary manual consumer project against the local package folder source,
   - but keep this as a **compile/package smoke check**, not the full fresh consumer acceptance.

## Forward Intelligence

- **Do not use `dotnet add package` inside an in-repo scratch consumer casually.**
  - Because this repo uses `Directory.Packages.props`, `dotnet add package` inside a temporary project under the repo tree tries to mutate central package management state.
  - During research I hit exactly this behavior and had to revert it.
  - If S02 or S03 needs a scratch consumer in or near this tree, prefer a **manually-authored `.csproj`** or create the consumer outside the central-package-management scope.

- **Version/caching strategy needs deliberate handling before S03.**
  - The current package version defaults to `1.0.0`.
  - Repacking the same version during later consumer-proof work can produce false confidence if NuGet cache behavior hides updated artifacts.
  - S02 should either set an intentional versioning strategy in the project file or at least document how S03 should override/uniquify package version during proof.

- **Avoid docs that imply SDK parity or drop-in replacement.**
  - D002 still applies: the product is an explicit resource-specific seam, not a raw Azure SDK clone.
  - Package docs should teach the focused factory pattern, not erase it.

- **Avoid docs that imply background Service Bus execution.**
  - S01 proved explicit rerun semantics; package-facing examples must keep that truthful.

- **Keep the docs single-sourced if possible.**
  - There is no existing docs infrastructure here.
  - A strong root `README.md` that is also packed as the package readme is less likely to drift than separate repo and package guides.

## Skill Discovery Suggestions

No installed skill in `<available_skills>` is directly about NuGet/package authoring, so no skill-specific implementation rules changed the plan for this slice.

I checked for directly relevant external skills and the most promising results were:

- `npx skills add aaronontheweb/dotnet-skills@package-management`
  - strongest install count among directly relevant .NET package-management results (`140 installs`)
  - best general fit if the user wants extra guidance around local package flows and restore behavior

- `npx skills add wshaddix/dotnet-skills@dotnet-nuget-authoring`
  - lower install count (`6 installs`) but the most directly aligned to NuGet/package authoring work
  - best fit if the user wants package-metadata/readme guidance specifically

- `npx skills add github/awesome-copilot@nuget-manager`
  - very high install count (`8.2K installs`)
  - broad NuGet-management coverage; promising if the user wants a more general NuGet helper rather than a package-authoring-specialized one

## Planner-ready task shape

If I were decomposing S02 next, I would split it like this:

1. **T01 — Package metadata hardening**
   - update `src/Azure.InMemory/Azure.InMemory.csproj`
   - include package readme + internal-ready metadata
   - decide versioning strategy that will not sabotage S03

2. **T02 — README/package quickstart authoring**
   - replace the placeholder root `README.md`
   - document install, DI registration, topology declaration, processor settlement, inspection state, and explicit redelivery semantics accurately

3. **T03 — Pack verification**
   - run solution tests + pack
   - confirm the package now contains the intended readme/metadata and no longer emits the missing-readme warning

That sequence retires the real risk in this slice without pulling S03’s fresh-consumer acceptance work forward.
