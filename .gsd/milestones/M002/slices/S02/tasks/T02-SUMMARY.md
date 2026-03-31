---
id: T02
parent: S02
milestone: M002
provides: []
requires: []
affects: []
key_files: ["README.md", ".gsd/KNOWLEDGE.md", ".gsd/milestones/M002/slices/S02/tasks/T02-SUMMARY.md"]
key_decisions: []
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran the task-plan verification command against README.md to prove the packaged guide now contains the required install, DI registration, seam, settlement, rerun, and inspection keywords. The command passed and showed the expected PackageReference, AddAzureServiceBusInMemory, Azure.InMemory.DependencyInjection, IAzureServiceBusFactory, CompleteMessageAsync, StartProcessingAsync, InMemoryServiceBusState, and canonical Subscriptions references in the document. As an intermediate task, this stops short of the slice-closing pack/test proof owned by T03."
completed_at: 2026-03-31T03:21:18.264Z
blocker_discovered: false
---

# T02: Replaced the placeholder package readme with a real Service Bus quickstart that teaches DI registration, explicit topology, deterministic reruns, and test-only inspection truthfully.

> Replaced the placeholder package readme with a real Service Bus quickstart that teaches DI registration, explicit topology, deterministic reruns, and test-only inspection truthfully.

## What Happened
---
id: T02
parent: S02
milestone: M002
key_files:
  - README.md
  - .gsd/KNOWLEDGE.md
  - .gsd/milestones/M002/slices/S02/tasks/T02-SUMMARY.md
key_decisions:
  - (none)
duration: ""
verification_result: passed
completed_at: 2026-03-31T03:21:18.266Z
blocker_discovered: false
---

# T02: Replaced the placeholder package readme with a real Service Bus quickstart that teaches DI registration, explicit topology, deterministic reruns, and test-only inspection truthfully.

**Replaced the placeholder package readme with a real Service Bus quickstart that teaches DI registration, explicit topology, deterministic reruns, and test-only inspection truthfully.**

## What Happened

Replaced the placeholder root README.md with a package-facing guide an internal consumer can follow from the published package boundary instead of a repo checkout. The new README now explains installation through PackageReference or dotnet add package, frames IAzureServiceBusFactory as the primary Service Bus seam, and shows the real using Azure.InMemory.DependencyInjection; plus services.AddAzureServiceBusInMemory() registration flow. Added concrete queue and topic/subscription quickstarts that declare topology through factory.Administration, send via CreateSender(...), and process through CreateQueueProcessor(...) or CreateSubscriptionProcessor(...) without inventing raw Azure SDK client usage for the application seam. Documented settlement and retry behavior to match the current tests exactly: successful handlers must call CompleteMessageAsync(...) unless AutoCompleteMessages: true is chosen, and failed deliveries reappear only on the next explicit StartProcessingAsync() run. Documented InMemoryServiceBusState as a test-only inspection surface and recorded in .gsd/KNOWLEDGE.md that public docs must use the literal canonical subscription path <topic>/Subscriptions/<subscription> because the helper that builds that string is internal.

## Verification

Ran the task-plan verification command against README.md to prove the packaged guide now contains the required install, DI registration, seam, settlement, rerun, and inspection keywords. The command passed and showed the expected PackageReference, AddAzureServiceBusInMemory, Azure.InMemory.DependencyInjection, IAzureServiceBusFactory, CompleteMessageAsync, StartProcessingAsync, InMemoryServiceBusState, and canonical Subscriptions references in the document. As an intermediate task, this stops short of the slice-closing pack/test proof owned by T03.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `test -s ./README.md && rg -n "PackageReference|AddAzureServiceBusInMemory|Azure.InMemory.DependencyInjection|IAzureServiceBusFactory|CompleteMessageAsync|StartProcessingAsync|InMemoryServiceBusState|Subscriptions" ./README.md` | 0 | ✅ pass | 28ms |


## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `README.md`
- `.gsd/KNOWLEDGE.md`
- `.gsd/milestones/M002/slices/S02/tasks/T02-SUMMARY.md`


## Deviations
None.

## Known Issues
None.
