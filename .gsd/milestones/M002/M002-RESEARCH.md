# M002: Fidelity and packaging — Research

**Date:** 2026-03-30

## Summary

M001 already established the right architecture for M002: explicit per-resource registration, focused library-owned seams, state-owned in-memory behavior, dedicated behavior suites, and test-only observability beyond the official Azure SDK surface. The highest-value M002 move is **not** broad new surface area. It is a narrow Service Bus fidelity step that deepens trust in the existing seam, followed by packaging proof from a real consumer boundary.

My recommendation is:

1. make **Service Bus redelivery / delivery-count / max-delivery dead-letter** the first fidelity target;
2. keep that work inside the existing `InMemoryServiceBusState` + thin factory/processor pattern;
3. treat **internal package consumption proof** as a separate acceptance track, not just package metadata cleanup;
4. keep **Blob Trigger deferred** unless a concrete consumer scenario forces it back in.

The biggest practical surprise is that this worktree does **not** currently contain the source tree (`src/`, `tests/`, `.sln`, `.csproj` were not present in direct inspection). So this research is grounded in:

- the preloaded M002 context,
- `.gsd/DECISIONS.md`,
- `.gsd/KNOWLEDGE.md`,
- M001 roadmap/slice research/summaries,
- and milestone closeout artifacts,

rather than a fresh direct read of the implementation files named in those artifacts.

## What I Could Verify Directly In This Worktree

Direct inspection from this worktree found:

- `.gsd/` planning and execution artifacts,
- `README.md`, `LICENSE`, `.gitignore`,
- **no** `Azure.InMemory.sln`,
- **no** `*.csproj`,
- **no** `src/` or `tests/` tree.

That matters for planning: the roadmap planner should assume the implementation checkout needs to be available before slice execution. I could still recover the shape of the existing system from M001 artifacts, but I could not re-verify current code details such as exact package metadata, current test count, or exact public signatures firsthand.

## Existing Patterns To Reuse

These M001 patterns look like the stable foundation for M002 and should be treated as continuity constraints unless a later milestone explicitly revisits them:

- **Explicit resource-specific registration** via `AddAzure*Sdk()` / `AddAzure*InMemory()`.
- **Focused resource seams** (`IAzureServiceBusFactory`, `IAzureBlobFactory`, `IAzureKeyVaultFactory`) instead of returning raw Azure SDK clients.
- **State-owned in-memory behavior** with thin adapters/factories over the state root.
- **Dedicated runtime behavior suites** per resource, with DI registration tests staying DI-focused.
- **Service Bus canonical subscription paths** using `<topic>/Subscriptions/<subscription>` as the processing/observability unit.
- **Processor snapshot-drain semantics** rather than background polling.
- **Test-only observability surfaces** as a first-class differentiator rather than an implementation leak to hide.

For Service Bus specifically, M001 already proved a useful baseline:

- declared topology is required before ingress;
- topic publish fans out into canonical subscription paths;
- processors drain the current pending snapshot synchronously;
- completed, dead-lettered, pending, and errored outcomes are state-backed and inspectable;
- handler exceptions are terminal errored outcomes, not hidden implicit retries.

That strongly suggests M002 fidelity should extend the same state machine rather than introduce a parallel transport model.

## Strategic Recommendation

### 1) What should be proven first

**Prove one realistic richer Service Bus scenario before packaging becomes the main focus.**

The best first proof target is:

- **redelivery after unsuccessful processing**,
- **observable delivery count increments**,
- **automatic dead-letter after max delivery count**.

Why this first:

- It is directly adjacent to the current M001 processor model.
- It catches realistic integration bugs that the MVP path does not catch.
- It can reuse the current state-owned lifecycle pattern.
- It improves credibility more than packaging polish alone.
- It is narrow enough to avoid turning M002 into a Service Bus clone.

### 2) What should stay out of the first M002 fidelity slice

These are possible later behaviors, but they look too broad or too architecture-shaping for the first M002 cut:

- sessions,
- lock renewal,
- long-running background processor simulation,
- true concurrent delivery semantics,
- scheduled messages,
- deferred messages,
- Blob Trigger host/runtime emulation.

Among the deferred Service Bus candidates, **scheduled/deferred messages** are less attractive as the first target because they pull in time/sequence-number semantics and retrieval APIs, while **lock/session/background execution** would push against the current snapshot-drain test model.

### 3) Packaging should be real consumer proof, not metadata theater

R022 should not be considered advanced merely because the `.csproj` has better package metadata. The milestone context is explicit: M002 is not done until the package is **packed and used from outside the producing project boundary**.

So the packaging bar should be:

- pack the library into a `.nupkg`,
- consume it from a fresh internal-style consumer project,
- restore it through a normal NuGet flow (local folder source is sufficient for internal-ready proof),
- run a meaningful local test flow through the packaged library,
- and prove the docs/examples are enough that the consumer does not have to guess how to wire `AddAzureServiceBusInMemory()`.

## Natural Slice Boundaries

## Slice A — Service Bus fidelity contract and proof

**Goal:** deepen Service Bus behavior without changing the public seam shape.

Likely responsibilities:

- choose and implement the retry/redelivery contract,
- add delivery-count tracking to the state model,
- define what causes redelivery vs terminal error vs dead-letter,
- add observability for delivery count and retry history,
- prove the richer behavior through dedicated Service Bus behavior tests.

What this slice should retire:

- the main M002 behavioral unknown,
- whether the existing state model is sufficient,
- whether the current processor lifecycle stays lean under deeper fidelity.

Done when a consumer-style test can show a message being retried/redelivered and then dead-lettered after the configured max deliveries, with state-backed evidence.

## Slice B — Package surface, docs, and examples

**Goal:** make the library packageable and understandable for internal consumers.

Likely responsibilities:

- finalize package metadata in the project file,
- write/update package-facing README/docs,
- add a minimal example or consumer snippet that mirrors real use,
- ensure `dotnet pack` produces a usable package artifact.

This slice should stay narrow: internal-ready, not public-distribution hardening.

## Slice C — External consumer installation proof

**Goal:** prove real package consumption from outside the producing project boundary.

Likely responsibilities:

- create or reuse a fresh consumer test project,
- restore the package from a local folder feed or equivalent internal-ready path,
- wire the package into a meaningful local test flow,
- run `dotnet test` successfully against the packaged library,
- capture the exact proof steps so another close-in consumer can repeat them.

This should be a distinct acceptance slice or explicit final step, because it validates something the library project alone cannot prove from its own project references.

## Optional Slice D — Ergonomics cleanup only if the previous slices expose real friction

Possible follow-up scope if needed:

- public-vs-test-only helper boundary cleanup,
- small API/documentation refinements,
- better diagnostics if package consumption reveals confusing failure paths.

This should be conditional, not assumed.

## Boundary Contracts That Matter

These contracts appear central to preserving continuity with M001:

### Architecture / seam

- Keep **resource-specific** registration and factory seams.
- Do **not** introduce a single omnibus provider abstraction.
- Do **not** return raw Azure SDK clients from the public seam.

### Service Bus behavior model

- Keep `InMemoryServiceBusState` as the truth surface for lifecycle and observability.
- Keep canonical subscription paths as the unit of topic-subscription processing.
- Keep new behavior additive to the current state machine rather than creating a side channel.
- Keep tests asserting through the shared state/harness, not through hidden timing behavior.

### Verification model

- Runtime behavior proof belongs in focused behavior suites.
- DI registration tests should stay DI-only.
- Supported scenarios must stay inside `dotnet test` with no Azure, Docker, or emulator dependency.

### Packaging acceptance

- Package-consumption proof must cross the producer/consumer boundary.
- A project reference is **not** enough to validate R022.

## Failure Modes That Should Shape Slice Ordering

### 1) Choosing the wrong first fidelity target

If the first fidelity slice chases scheduled/deferred/session semantics, M002 risks expanding into new API/time/host models before retiring the most valuable trust gap. That would delay proof and create more rework risk.

### 2) Letting fidelity escape into the public seam

If retries/redelivery require widening the public API too early, the project may drift away from the lean “EF Core InMemory for Azure-like testing” direction and lock in a more clone-like surface.

### 3) Hiding retries instead of exposing them

If M002 simulates retries/redelivery without exposing delivery counts/history through the test harness, the library will gain behavior but lose one of its main differentiators: strong failure visibility.

### 4) Treating package metadata as package proof

A prettier `.csproj` without a fresh consumer restore/install/use flow would leave the milestone only partially proven.

### 5) Stale package cache false confidence

Package-consumption proof can be misleading if the consumer restores a cached build of the package. The proof flow should use a unique version/prerelease suffix or explicitly clear/avoid cache confusion.

### 6) Source checkout mismatch

This worktree currently lacks the implementation tree, so downstream execution work will fail unless the actual source checkout is present. That is an execution/setup risk, not a product risk, but it should be surfaced early.

## Requirements Analysis

## Current requirement posture

`REQUIREMENTS.md` currently has **no active requirements**. For M002 planning, that means the roadmap must deliberately reactivate or newly map the deferred work instead of assuming scope will “just happen.”

### Table stakes for M002

These look like the real table stakes:

- **R020 — Advanced Service Bus fidelity**
- **R022 — NuGet publication readiness** (clarified in context as **internal-ready only**)

### Explicit non-table-stakes for M002

This should stay deferred unless the user explicitly reopens it:

- **R021 — Azure Functions blob-trigger integration**

The context is already consistent on this point: Blob Trigger is the major scope-creep risk and should not silently become required work.

## Missing / candidate requirements to consider during planning

These should stay advisory unless the planner/user explicitly promotes them into requirements.

### Candidate requirement A — Observable redelivery and delivery count

The in-memory Service Bus provider should simulate retry/redelivery for unsuccessfully processed messages, expose delivery count per message, and automatically dead-letter after a configured maximum delivery threshold.

Why it matters:

- It is the most natural next step after M001’s processor basics.
- It catches realistic consumer mistakes.
- It maps cleanly to R020.

### Candidate requirement B — Deterministic retry proof inside the current processor model

The retry/redelivery model should remain deterministic and testable under the current explicit processor execution model, likely by using repeated processor runs over state-backed pending work rather than introducing timing-dependent background loops.

Why it matters:

- It preserves R003 and the current testing style.
- It reduces flakiness and keeps the architecture lean.

### Candidate requirement C — Internal package consumer success path

A fresh internal consumer should be able to install the packaged library, follow the provided docs/example, register the in-memory Service Bus provider, and run a meaningful local test flow without guessing hidden setup steps.

Why it matters:

- It is the concrete user-facing interpretation of R022 in this milestone.
- It turns package readiness into proof rather than aspiration.

## Behaviors that look optional, not required

Unless the user says otherwise, these look optional or later-phase:

- scheduled messages,
- deferred messages,
- lock renewal semantics,
- session support,
- public NuGet release hardening,
- SourceLink/symbol/package-signing quality bars,
- internal-feed publishing beyond a local-folder-feed proof,
- Blob Trigger host/runtime emulation.

## Continuity expectations from validated M001 requirements

Even though M002 will probably only reactivate R020/R022, the planner should treat these validated behaviors as continuity constraints:

- R001/R002/R004: explicit resource seams and SDK-backed adapters stay intact;
- R003: the supported loop still runs in-process under `dotnet test`;
- R005/R006/R007/R010: Service Bus behavior remains truthful and observable, not hidden behind opaque simulation.

## Skill Discovery Suggestions

No installed skill in `<available_skills>` is a direct Azure Service Bus or NuGet-packaging specialist, but these are the most relevant findings.

### Already available in this environment

- `error-handling-patterns` — directly relevant if M002 models redelivery, terminal failure, and dead-letter transitions.

### Promising uninstalled skills

- **Azure Service Bus (.NET)**
  - `npx skills add sickn33/antigravity-awesome-skills@azure-servicebus-dotnet`
  - Why promising: highest install count among the Azure Service Bus–specific results I found and directly aligned to the likely M002 fidelity target.

- **.NET package management / NuGet flow**
  - `npx skills add aaronontheweb/dotnet-skills@package-management`
  - Why promising: strongest install count among package-management/NuGet-related results and directly relevant to internal package consumption proof.

- **NuGet authoring**
  - `npx skills add wshaddix/dotnet-skills@dotnet-nuget-authoring`
  - Why promising: specifically packaging-oriented, lower install count than the package-management skill but closer to the `.nupkg` authoring problem.

- **.NET testing**
  - `npx skills add novotnyllc/dotnet-artisan@dotnet-testing`
  - Why promising: useful if the consumer-boundary verification slice needs cleaner test-project setup and proof discipline.

## Planning Guidance For The Roadmap Planner

If the planner wants the safest milestone shape, I would bias toward:

1. **Service Bus fidelity first** — pick one narrow realism upgrade and prove it hard.
2. **Package authoring/docs second** — prepare the artifact and instructions.
3. **External consumer proof last** — validate that the package and docs actually work outside the producer project.

That order retires the milestone’s biggest product risk before the finishing work, and it preserves a clean final acceptance story:

- richer in-memory Service Bus behavior is real,
- the package is real,
- and a real consumer can use it.

## Research Gaps / Caveats

- I could not directly inspect the current source or test files because they are absent from this worktree checkout.
- I therefore could not validate the exact current package metadata or determine whether any M002-adjacent work has already started in code.
- The planner should treat the M001 summaries/decisions as the best available technical truth until an execution worktree with the full source tree is present.
