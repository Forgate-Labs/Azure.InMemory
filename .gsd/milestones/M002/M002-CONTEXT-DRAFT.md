---
depends_on: [M001]
---

# M002: Fidelity and packaging — Context Draft

**Gathered:** 2026-03-30
**Status:** Draft for later discussion

> This is a draft, not a finalized context. It captures seed material from the multi-milestone discussion so a future `/gsd` discussion can resume without losing intent.

## What Was Already Decided

- M001 is the primary milestone and owns the first useful MVP.
- The architecture seam is resource-specific provider registration plus focused factories, not drop-in SDK replacement.
- The project intentionally accepts a small refactor cost in consumer projects to gain a clean provider seam.
- The public composition style is explicit and verbose on purpose, e.g. `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()`.
- Azure Functions Blob Trigger support is not part of M001.
- NuGet publication should wait until the implementation level feels good enough, likely near the end of M002.

## Working Intent For M002

M002 likely deepens the usefulness and credibility of the library after the M001 seam and MVP are proven. The current read is that it should focus on richer behavioral fidelity, deferred resource scenarios, better ergonomics, and packaging readiness.

## Likely Scope

### In Scope candidates

- richer Service Bus fidelity beyond the basic send/receive/complete/dead-letter path
- better failure visibility and test ergonomics where M001 exposes friction
- deferred Blob-related capabilities that extend usefulness without dragging in a full host/runtime model too early
- packaging, docs, and polish needed before publishing the library as a NuGet package

### Explicitly deferred from M001 and likely reconsidered here

- advanced Service Bus semantics such as retries, delivery count, and other higher-fidelity behaviors
- Azure Functions Blob Trigger style integration in memory
- publication readiness for external package consumption

## Why This Milestone Exists

M001 proves the seam and the first useful end-to-end path. M002 exists so the project does not stop at a thin MVP. The user wants the library to improve after the initial milestone rather than trying to cram all fidelity into the first release.

## What This Milestone Unlocks

- stronger trust that in-memory behavior catches more realistic integration mistakes
- broader usefulness across more test scenarios
- a quality level that could justify NuGet publication

## Dependencies

This milestone depends on M001. The seam, focused factories, and initial in-memory implementations need to exist before fidelity and packaging work can be planned properly.

## Risks / Unknowns To Revisit In The Future Discussion

- Which Service Bus fidelity improvements matter most after M001 lands in code
- Whether Blob Trigger support should stay in M002 or move later if it starts pulling in too much runtime complexity
- What exact bar should trigger NuGet publication
- Which harness APIs from M001 should remain public versus test-only helpers

## Current Planning Bias

- Keep M002 as an improvement milestone, not a re-architecture milestone
- Prefer deepening real consumer scenarios over broadening the surface area too quickly
- Preserve the project's "EF Core InMemory for Azure-like testing" feel without turning it into an Azure SDK clone

## Next Discussion Starting Points

When M002 becomes active, the future discussion should answer:

1. Which advanced Service Bus behaviors are most worth simulating next
2. Whether Blob Trigger support still feels like the next best addition
3. What packaging, docs, and examples are required before NuGet publication
4. Which real consumer projects should be used as fidelity reference cases
