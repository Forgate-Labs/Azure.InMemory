# S01: Observable Service Bus redelivery fidelity — UAT

**Milestone:** M002
**Written:** 2026-03-31T02:55:42.768Z

# S01 UAT — Observable Service Bus redelivery fidelity

## Preconditions
- Working directory: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M002`
- .NET 10 SDK is installed.
- The active root contains `./Azure.InMemory.sln`, `./src/Azure.InMemory/...`, and `./tests/Azure.InMemory.Tests/...`.
- No Azure resources, Docker containers, or external emulators are required.
- Run the commands sequentially from this worktree using the explicit relative solution path `./Azure.InMemory.sln`.

## Test Case 1 — Queue failure is retried only on the next explicit processor run
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.QueueFailureRequeuesOnlyForTheNextExplicitRunAndIncrementsDeliveryCount`.
   - Expected: the test passes.
2. Confirm the behavior embodied by the passing test.
   - Expected: the first `StartProcessingAsync()` run throws from the handler, records one errored outcome for delivery count 1, and leaves exactly one pending queue envelope with `DeliveryCount = 2` and `MaxDeliveryCount = 3`.
3. Run the same processor again through the passing test scenario.
   - Expected: the second explicit run sees `context.DeliveryCount == 2`, completes the message, empties the pending queue, and does not create a dead-letter outcome.
4. Acceptance check.
   - Expected: retry happens only after the second explicit processor run begins; no background polling or same-run duplicate delivery occurs.

## Test Case 2 — Queue poison messages dead-letter at the configured maximum without leaving duplicates
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.QueueFailureDeadLettersOnceConfiguredMaxDeliveryCountIsExhausted`.
   - Expected: the test passes.
2. Confirm the first failed-delivery state embodied by the test.
   - Expected: after the first run, the queue still has one pending envelope with `DeliveryCount = 2`, while completed and dead-letter stores remain empty.
3. Confirm the exhaustion behavior embodied by the test.
   - Expected: after the second explicit run fails, the queue has no pending envelope, `GetDeadLetteredMessages("orders")` contains exactly one outcome with `DeliveryCount = 2`, `MaxDeliveryCount = 2`, and a dead-letter reason containing `MaxDeliveryCount exhausted`, and `GetErroredMessages("orders")` contains one outcome per failed attempt.
4. Edge check.
   - Expected: no duplicate pending copy survives once the final failure dead-letters the message.

## Test Case 3 — Canonical subscription paths retry independently from sibling subscriptions
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.SubscriptionFailureRequeuesOnlyItsCanonicalPathForTheNextExplicitRunAndIncrementsDeliveryCount`.
   - Expected: the test passes.
2. Confirm the topic fan-out state embodied by the passing test.
   - Expected: publishing to topic `orders` creates independent pending copies under `orders/Subscriptions/billing` and `orders/Subscriptions/shipping`, not under the topic path itself.
3. Confirm the retry isolation embodied by the passing test.
   - Expected: after the billing subscription fails once, only `orders/Subscriptions/billing` advances to `DeliveryCount = 2` and records an errored outcome; the sibling shipping subscription keeps its untouched pending copy with `DeliveryCount = 1` and no errored or dead-letter outcomes.
4. Confirm the second-run completion embodied by the passing test.
   - Expected: the next explicit billing processor run sees delivery count 2, completes the message, and still leaves the untouched sibling copy pending for its own processor.

## Test Case 4 — One exhausted subscription clone dead-letters without poisoning its sibling
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.SubscriptionFailureDeadLettersOnlyTheExhaustedCanonicalPathAndLeavesSiblingSubscriptionDeliverable`.
   - Expected: the test passes.
2. Confirm the exhausted-path behavior embodied by the passing test.
   - Expected: repeated failures on `orders/Subscriptions/billing` advance only that canonical path to `DeliveryCount = 2`, then dead-letter that one clone with a `MaxDeliveryCount exhausted` reason and no leftover pending copy.
3. Confirm the sibling-path behavior embodied by the same test.
   - Expected: `orders/Subscriptions/shipping` still contains its original pending copy with `DeliveryCount = 1` until its own processor runs.
4. Confirm sibling deliverability.
   - Expected: when the shipping processor runs, it completes successfully on delivery count 1 and records one completed outcome, proving sibling subscription clones were not exhausted or mutated by the billing failures.

## Test Case 5 — Invalid max-delivery configuration fails fast
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests.QueueProcessorCreationRejectsInvalidMaxDeliveryCounts`.
   - Expected: the test passes for both `0` and `-1`.
2. Confirm the failure semantics embodied by the passing test.
   - Expected: queue processor creation throws `ArgumentOutOfRangeException` that names `AzureServiceBusProcessorOptions.MaxDeliveryCount` instead of accepting invalid retry configuration and failing later during processing.

## Final Slice Proof
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusRedeliveryTests`.
   - Expected: the focused redelivery suite passes.
2. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`.
   - Expected: the broader Service Bus regression surface passes.
3. Run `dotnet test ./Azure.InMemory.sln`.
   - Expected: the entire solution passes from the active M002 root with no external infrastructure.
4. Confirm the overall result.
   - Expected: the slice proves that an unsuccessfully processed queue or canonical subscription message is redelivered only on the next explicit processor run with incremented delivery count and automatically dead-lettered once the configured maximum is exhausted, while all evidence remains visible through the existing in-memory harness.

## Failure Signals
- Any focused redelivery test failure involving wrong `DeliveryCount`, missing requeue, missing dead-letter, wrong dead-letter reason, or a leftover pending duplicate after exhaustion.
- Any Service Bus regression failure showing that ingress/processor behavior no longer preserves canonical subscription paths, explicit rerun semantics, or existing settlement expectations.
- Any full-solution `dotnet test ./Azure.InMemory.sln` failure, which would indicate the slice regressed the infrastructure-free loop used by later packaging work.

## Not Proven By This UAT
- Real Azure Service Bus broker behavior, lock renewal, sessions, scheduling, duplicate detection, or background processor loops.
- Package-production and external-consumer flows; those remain the responsibility of M002/S02 and M002/S03.

## Notes for Tester
- Run the verification commands sequentially from this worktree. Shared `bin/obj` outputs can create noisy copy-retry warnings when multiple `dotnet test` commands run in parallel.
- Use the explicit relative solution path `./Azure.InMemory.sln`; this worktree previously had a root-checkout mismatch, and the authoritative proof now belongs to the restored M002 root rather than any sibling worktree copy.
