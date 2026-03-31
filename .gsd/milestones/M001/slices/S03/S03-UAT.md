# S03: Processor execution and settlement observability — UAT

**Milestone:** M001
**Written:** 2026-03-30T22:14:04.227Z

# S03: Processor execution and settlement observability — UAT

**Milestone:** M001
**Written:** 2026-03-30T17:17:44-03:00

# S03 UAT — Processor execution and settlement observability

## Preconditions
- Working directory: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M001`
- .NET 10 SDK is installed.
- No Azure resources, Docker containers, or external emulators are required.
- The solution is built from the same worktree so the in-memory Service Bus processor tests and shared state reflect the current slice code.

## Test Case 1 — Queue processor explicitly completes a stored envelope and preserves metadata
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueStartProcessingCompletesExplicitlySettledMessagesAndPreservesEnvelopeMetadata`.
   - Expected: the test passes.
2. Confirm the setup embodied by the passing test.
   - Expected: `CreateQueueAsync("orders")` declares the queue, `SendAsync(...)` stores one pending envelope, and the processor reads that envelope through `ProcessMessageAsync`.
3. Confirm the handler behavior embodied by the test.
   - Expected: the handler sees body `hello queue`, `MessageId` `message-001`, and application properties `tenant=tenant-a` and `attempt=2`, then calls `CompleteMessageAsync(...)`.
4. Confirm the post-processing state embodied by the test.
   - Expected: `GetPendingMessages("orders")`, `GetDeadLetteredMessages("orders")`, and `GetErroredMessages("orders")` are empty, while `GetCompletedMessages("orders")` contains exactly one preserved envelope with the same body and metadata.

## Test Case 2 — Queue processor leaves a successful but unsettled message pending when auto-complete is disabled
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueStartProcessingLeavesSuccessfulUnsettledMessagesPendingWhenAutoCompleteDisabled`.
   - Expected: the test passes.
2. Confirm the handler behavior embodied by the passing test.
   - Expected: `ProcessMessageAsync` returns successfully without calling either settlement method.
3. Confirm the resulting state embodied by the test.
   - Expected: `GetPendingMessages("orders")` still contains the original envelope (`message-002` / `pending queue`), and the completed, dead-lettered, and errored outcome stores remain empty.
4. Edge check.
   - Expected: the message is requeued once for the next processor run rather than silently completed or dropped.

## Test Case 3 — Queue processor auto-completes a successful unsettled message when enabled
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueStartProcessingAutoCompletesSuccessfulUnsettledMessagesWhenEnabled`.
   - Expected: the test passes.
2. Confirm the processor configuration embodied by the passing test.
   - Expected: `CreateQueueProcessor("orders", new AzureServiceBusProcessorOptions(AutoCompleteMessages: true))` enables auto-complete.
3. Confirm the resulting state embodied by the test.
   - Expected: the queue has no pending, dead-lettered, or errored messages after processing, and `GetCompletedMessages("orders")` contains exactly one envelope for `message-003` / `auto complete queue`.

## Test Case 4 — Undeclared processors fail loudly instead of inventing topology
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueStartProcessingFailsForUndeclaredQueuesWithActionableDiagnostics`.
   - Expected: the test passes.
2. Confirm the queue failure semantics embodied by the test.
   - Expected: starting a queue processor for `missing-queue` throws an `InvalidOperationException` that includes the queue name and references `IAzureServiceBusAdministration.CreateQueueAsync`.
3. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.SubscriptionStartProcessingFailsForUndeclaredSubscriptionsWithActionableDiagnostics`.
   - Expected: the test passes.
4. Confirm the subscription failure semantics embodied by the test.
   - Expected: starting a subscription processor for `orders/Subscriptions/billing` throws an `InvalidOperationException` that includes the canonical entity path and references `IAzureServiceBusAdministration.CreateSubscriptionAsync`.
5. Edge check.
   - Expected: neither failure path creates pending, completed, dead-lettered, or errored state for the missing entity.

## Test Case 5 — Subscription processing dead-letters on the canonical path and preserves the reason
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.SubscriptionStartProcessingDeadLettersPublishedMessagesOnCanonicalPathAndPreservesReason`.
   - Expected: the test passes.
2. Confirm the ingress and topology embodied by the passing test.
   - Expected: a message published to topic `orders` is stored only on `orders/Subscriptions/billing`, not on the topic path itself.
3. Confirm the handler behavior embodied by the passing test.
   - Expected: `ProcessMessageAsync` calls `DeadLetterMessageAsync("poison-order")`.
4. Confirm the resulting state embodied by the test.
   - Expected: `GetPendingMessages("orders")` and `GetPendingMessages("orders/Subscriptions/billing")` are empty, `GetCompletedMessages(...)` and `GetErroredMessages(...)` stay empty, and `GetDeadLetteredMessages("orders/Subscriptions/billing")` contains exactly one preserved outcome with `MessageId` `message-101`, body `hello topic`, preserved application properties, and `DeadLetterReason` `poison-order`.

## Test Case 6 — Handler exceptions become inspectable errored outcomes and still notify `ProcessErrorAsync`
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.SubscriptionStartProcessingRecordsHandlerExceptionsAndInvokesProcessErrorAsync`.
   - Expected: the test passes.
2. Confirm the handler behavior embodied by the passing test.
   - Expected: `ProcessMessageAsync` throws `InvalidOperationException("boom from subscription handler")`.
3. Confirm the error callback behavior embodied by the test.
   - Expected: `ProcessErrorAsync` receives an `AzureServiceBusErrorContext` whose `EntityPath` is `orders/Subscriptions/shipping` and whose `Exception` is the same thrown instance.
4. Confirm the resulting state embodied by the test.
   - Expected: the canonical subscription path has no pending, completed, or dead-lettered messages, and `GetErroredMessages("orders/Subscriptions/shipping")` contains exactly one outcome that preserves the envelope metadata plus the thrown exception.
5. Edge check.
   - Expected: `GetErroredMessages("orders")` remains empty because the topic path is still only a routing key, not a processing buffer.

## Test Case 7 — Invalid second settlement is surfaced deterministically without duplicating the first outcome
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.SubscriptionStartProcessingCapturesInvalidSettlementOrderingWithoutDuplicatingDeadLetterOutcome`.
   - Expected: the test passes.
2. Confirm the handler behavior embodied by the passing test.
   - Expected: the handler dead-letters the message first and then attempts to complete it.
3. Confirm the resulting state embodied by the test.
   - Expected: `GetDeadLetteredMessages("orders/Subscriptions/fraud")` contains exactly one dead-letter outcome for `message-104` with reason `fraud-detected`, `GetCompletedMessages(...)` stays empty, and `GetErroredMessages(...)` contains exactly one `InvalidOperationException` describing the invalid second settlement attempt.
4. Acceptance note.
   - Expected: the message is not duplicated across outcome stores beyond the intentional first dead-letter plus terminal error record.

## Test Case 8 — Queue handler exceptions are still recorded when no error callback is configured
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueStartProcessingRecordsHandlerExceptionsWhenProcessErrorAsyncIsNotConfigured`.
   - Expected: the test passes.
2. Confirm the resulting state embodied by the test.
   - Expected: `GetPendingMessages("orders")`, `GetCompletedMessages("orders")`, and `GetDeadLetteredMessages("orders")` are empty, while `GetErroredMessages("orders")` contains exactly one outcome for `message-103` whose exception message is `boom without callback`.

## Test Case 9 — Blank processor names still fail fast through argument validation
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.QueueProcessorCreationRejectsBlankQueueNames`.
   - Expected: the test passes for empty, space, and tab queue names.
2. Confirm the thrown error semantics embodied by the passing test.
   - Expected: each failure is an `ArgumentException` that names `queueName`.

## Final Slice Proof
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests.Queue`.
   - Expected: the queue-focused processor suite passes.
2. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~InMemoryServiceBusProcessorTests`.
   - Expected: the full processor suite passes.
3. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBus`.
   - Expected: the broader Service Bus regression suite passes.
4. Run `dotnet test ./Azure.InMemory.sln`.
   - Expected: the entire solution passes with no external infrastructure.
5. Confirm the overall result.
   - Expected: the slice proves that an in-memory processor can consume a published or enqueued message and leave a truthful, inspectable completed, dead-lettered, pending, or errored result entirely inside `dotnet test`.

## Acceptance Notes
- This slice is accepted when all commands above pass locally from this worktree.
- A topic publish only counts as processed correctly when the processor consumes from the canonical subscription entity path and the topic name itself remains free of processing outcomes.
- Any regression that silently invents topology, swallows handler exceptions, or allows duplicate settlement without an explicit errored outcome is a slice failure, even if some message still appears processed later.
