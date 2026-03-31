# S02: In-memory Service Bus topology and message ingress — UAT

**Milestone:** M001
**Written:** 2026-03-30T21:42:28.926Z

# S02 UAT — In-memory Service Bus topology and message ingress

## Preconditions
- Working directory: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M001`
- .NET 10 SDK is installed.
- No Azure resources, Docker containers, or external emulators are required.
- The solution is built from the same worktree so the in-memory Service Bus tests and shared state use the current slice code.

## Test Case 1 — Declared queue ingress stores one pending envelope with preserved metadata
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.QueueSendToDeclaredQueueStoresOnePendingEnvelopeWithBodyAndMetadata`.
   - Expected: the test passes.
2. Review the behavior embodied by the passing test.
   - Expected: `CreateQueueAsync("orders")` makes `QueueExists("orders")` true and leaves the queue empty before send.
3. Confirm the post-send state asserted by the test.
   - Expected: `GetPendingMessages("orders")` contains exactly one envelope whose `EntityPath` is `orders`, whose body is `hello queue`, whose `MessageId` is `message-001`, and whose application properties include `tenant=test-suite` and `attempt=2`.
4. Edge check.
   - Expected: the queue count changes from 0 to 1 only after the send; the sender does not create or mutate topology implicitly.

## Test Case 2 — Undeclared queue send fails loudly and does not create a pending buffer
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownQueueSendFailsWithActionableDiagnostics`.
   - Expected: the test passes.
2. Confirm the error semantics embodied by the passing test.
   - Expected: sending to `missing-queue` throws an `InvalidOperationException` whose message includes the queue name and references `IAzureServiceBusAdministration.CreateQueueAsync`.
3. Confirm the state assertions embodied by the passing test.
   - Expected: `QueueExists("missing-queue")` remains false and `GetPendingMessages("missing-queue")` remains empty.

## Test Case 3 — Declared topic publish fans out to subscription entity paths and leaves the topic path empty
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.TopicPublishFansOutBatchToDeclaredSubscriptionPathsAndLeavesTopicPathEmpty`.
   - Expected: the test passes.
2. Confirm the topology setup covered by the test.
   - Expected: the slice creates topic `orders` plus subscriptions `billing` and `shipping`, and the canonical entity paths are `orders/Subscriptions/billing` and `orders/Subscriptions/shipping`.
3. Confirm the post-publish assertions embodied by the test.
   - Expected: `GetPendingMessages("orders")` is still empty after publish, while each subscription path contains two pending envelopes.
4. Confirm envelope fidelity.
   - Expected: both subscription paths receive copies of `message-101` and `message-102` with preserved body text plus `tenant` and `attempt` application properties.
5. Edge check.
   - Expected: the billing and shipping envelopes are independent object instances rather than shared mutable references.

## Test Case 4 — Undeclared topic publish fails loudly and does not invent a topic queue
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.UnknownTopicPublishFailsWithActionableDiagnosticsAndDoesNotCreateTopicQueue`.
   - Expected: the test passes.
2. Confirm the error semantics embodied by the passing test.
   - Expected: publishing to `missing-topic` throws an `InvalidOperationException` whose message includes the topic name and references `IAzureServiceBusAdministration.CreateTopicAsync`.
3. Confirm the state assertions embodied by the passing test.
   - Expected: `TopicExists("missing-topic")` remains false and `GetPendingMessages("missing-topic")` stays empty.

## Test Case 5 — Blank topology names still fail fast through argument validation
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.QueueCreationRejectsBlankQueueNames`.
   - Expected: the test passes for empty, space, and tab queue names.
2. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.TopicCreationRejectsBlankTopicNames`.
   - Expected: the test passes for empty, space, and tab topic names.
3. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.SubscriptionCreationRejectsBlankTopicOrSubscriptionNames`.
   - Expected: the test passes for blank topic and subscription inputs and the thrown `ArgumentException` names the correct offending parameter.

## Test Case 6 — Processor startup remains the explicit deferred boundary for S03
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests.SubscriptionProcessorStartProcessingRemainsDeferredBoundary`.
   - Expected: the test passes.
2. Confirm the boundary semantics embodied by the passing test.
   - Expected: calling `StartProcessingAsync()` for `orders/Subscriptions/billing` throws a `NotSupportedException` whose message includes the entity path and explains that in-memory processing is not implemented yet.
3. Acceptance note.
   - Expected: this is the correct S02 boundary. A passing test means the slice stayed honest about ingress-only support rather than silently pretending processor execution exists.

## Final Slice Proof
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusIngressTests`.
   - Expected: the full ingress suite passes.
2. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`.
   - Expected: the broader Service Bus regression suite passes.
3. Run `dotnet test ./Azure.InMemory.sln`.
   - Expected: the entire solution passes with no external infrastructure.
4. Confirm the overall result.
   - Expected: the slice proves explicit topology creation, truthful queue ingress, truthful topic fan-out, preserved message metadata, actionable failure diagnostics for undeclared topology, and an unchanged processor boundary ready for S03.

## Acceptance Notes
- This slice is accepted when all commands above pass locally from this worktree.
- A topic publish only counts as correct when pending messages appear on canonical subscription entity paths and the topic name itself stays empty.
- A failure that silently creates a queue or topic buffer is a slice regression even if a test still receives a message later, because S02’s contract is topology-aware ingress rather than permissive buffering.
