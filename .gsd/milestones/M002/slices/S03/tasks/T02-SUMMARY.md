---
id: T02
parent: S03
milestone: M002
provides: []
requires: []
affects: []
key_files: ["samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs", "samples/Azure.InMemory.ExternalConsumer/README.md", ".gsd/KNOWLEDGE.md"]
key_decisions: ["Mirrored the producer redelivery scenario from the external consumer boundary by using only `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, `AzureServiceBusProcessorOptions`, and `InMemoryServiceBusState`, with no project references or repo-only helpers.", "Treated the failing processor run as a state-observation proof instead of an exception-flow proof because the in-memory seam records handler failures into errored outcomes and `ProcessErrorAsync` rather than rethrowing from `StartProcessingAsync()`."]
patterns_established: []
drill_down_paths: []
observability_surfaces: []
duration: ""
verification_result: "Ran `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore --filter FullyQualifiedName~ExternalConsumerQueueRedeliveryTests` and all three external-consumer tests passed. Ran `test -s ./samples/Azure.InMemory.ExternalConsumer/README.md && rg -n "AddAzureServiceBusInMemory|IAzureServiceBusFactory|DeliveryCount|StartProcessingAsync" ./samples/Azure.InMemory.ExternalConsumer/README.md` and confirmed the README documents the expected public seam. As an intermediate slice-status guardrail check, re-verified the sample has no `ProjectReference`, remains outside `Azure.InMemory.sln`, and still carries its committed README."
completed_at: 2026-03-31T12:44:19.279Z
blocker_discovered: false
---

# T02: Added package-only external-consumer tests that prove queue redelivery state and explicit second-run completion through the published Service Bus seam.

> Added package-only external-consumer tests that prove queue redelivery state and explicit second-run completion through the published Service Bus seam.

## What Happened
---
id: T02
parent: S03
milestone: M002
key_files:
  - samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs
  - samples/Azure.InMemory.ExternalConsumer/README.md
  - .gsd/KNOWLEDGE.md
key_decisions:
  - Mirrored the producer redelivery scenario from the external consumer boundary by using only `AddAzureServiceBusInMemory()`, `IAzureServiceBusFactory`, `AzureServiceBusProcessorOptions`, and `InMemoryServiceBusState`, with no project references or repo-only helpers.
  - Treated the failing processor run as a state-observation proof instead of an exception-flow proof because the in-memory seam records handler failures into errored outcomes and `ProcessErrorAsync` rather than rethrowing from `StartProcessingAsync()`.
duration: ""
verification_result: passed
completed_at: 2026-03-31T12:44:19.281Z
blocker_discovered: false
---

# T02: Added package-only external-consumer tests that prove queue redelivery state and explicit second-run completion through the published Service Bus seam.

**Added package-only external-consumer tests that prove queue redelivery state and explicit second-run completion through the published Service Bus seam.**

## What Happened

Replaced the placeholder consumer test with a real external-boundary xUnit suite in `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs`. The main proof test now builds a fresh DI container, registers `AddAzureServiceBusInMemory()`, resolves `IAzureServiceBusFactory`, declares queue topology, sends a real `ServiceBusMessage`, fails the first processor invocation intentionally, and asserts from `InMemoryServiceBusState` that the first run leaves one pending retryable message with `DeliveryCount == 2`, one errored outcome for the first failed delivery, and no completed message yet. It then calls `StartProcessingAsync()` a second time and proves that explicit rerun is the moment the message completes with `DeliveryCount == 2`. I also added negative consumer-boundary checks for undeclared topology and wrong-queue processing, updated the sample README to explain why this is meaningful package proof, and recorded the non-obvious runtime rule that handler failures are observed through state and `ProcessErrorAsync` rather than by catching a thrown exception from the failing run.

## Verification

Ran `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore --filter FullyQualifiedName~ExternalConsumerQueueRedeliveryTests` and all three external-consumer tests passed. Ran `test -s ./samples/Azure.InMemory.ExternalConsumer/README.md && rg -n "AddAzureServiceBusInMemory|IAzureServiceBusFactory|DeliveryCount|StartProcessingAsync" ./samples/Azure.InMemory.ExternalConsumer/README.md` and confirmed the README documents the expected public seam. As an intermediate slice-status guardrail check, re-verified the sample has no `ProjectReference`, remains outside `Azure.InMemory.sln`, and still carries its committed README.

## Verification Evidence

| # | Command | Exit Code | Verdict | Duration |
|---|---------|-----------|---------|----------|
| 1 | `DOTNET_CLI_UI_LANGUAGE=en dotnet test ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj --no-restore --filter FullyQualifiedName~ExternalConsumerQueueRedeliveryTests` | 0 | ✅ pass | 2439ms |
| 2 | `test -s ./samples/Azure.InMemory.ExternalConsumer/README.md && rg -n "AddAzureServiceBusInMemory|IAzureServiceBusFactory|DeliveryCount|StartProcessingAsync" ./samples/Azure.InMemory.ExternalConsumer/README.md` | 0 | ✅ pass | 11ms |
| 3 | `! rg -q "ProjectReference" ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj && ! rg -q "Azure.InMemory.ExternalConsumer" ./Azure.InMemory.sln && test -s ./samples/Azure.InMemory.ExternalConsumer/README.md` | 0 | ✅ pass | 17ms |


## Deviations

None.

## Known Issues

`bash ./scripts/verify-s03-external-consumer.sh` still does not exist in this worktree because that end-to-end producer-to-package-to-consumer automation belongs to T03.

## Files Created/Modified

- `samples/Azure.InMemory.ExternalConsumer/ExternalConsumerQueueRedeliveryTests.cs`
- `samples/Azure.InMemory.ExternalConsumer/README.md`
- `.gsd/KNOWLEDGE.md`


## Deviations
None.

## Known Issues
`bash ./scripts/verify-s03-external-consumer.sh` still does not exist in this worktree because that end-to-end producer-to-package-to-consumer automation belongs to T03.
