# Requirements

This file is the explicit capability and coverage contract for the project.

Use it to track what is actively in scope, what has been validated by completed work, what is intentionally deferred, and what is explicitly out of scope.

Guidelines:
- Keep requirements capability-oriented, not a giant feature wishlist.
- Requirements should be atomic, testable, and stated in plain language.
- Every **Active** requirement should be mapped to a slice, deferred, blocked with reason, or moved out of scope.
- Research may suggest requirements, but research does not silently make them binding.
- Validation means the requirement was actually proven by completed work and verification, not just discussed.

## Active

None currently.

## Validated

### R001 — Resource-specific provider registration
- Class: launchability
- Status: validated
- Description: Test projects must be able to register real Azure SDK-backed providers or in-memory providers separately for each supported resource.
- Why it matters: This is the seam that makes the library usable in real test suites without forcing one global mode.
- Source: user
- Primary owning slice: M001/S01
- Supporting slices: none
- Validation: S01 registration and mixed-composition tests prove each resource can choose SDK or in-memory registration independently via explicit `AddAzure*` methods.
- Notes: Public registrations are intentionally explicit and resource-scoped, e.g. `AddAzureServiceBusSdk()` and `AddAzureServiceBusInMemory()`.

### R002 — Focused factories per Azure resource
- Class: core-capability
- Status: validated
- Description: The library must expose focused factories per resource rather than one large provider abstraction.
- Why it matters: Smaller contracts keep the mini-framework enxuto and let projects inject only what they need.
- Source: user
- Primary owning slice: M001/S01
- Supporting slices: none
- Validation: S01 exposes focused `IAzureServiceBusFactory`, `IAzureBlobFactory`, and `IAzureKeyVaultFactory` seams and verifies them directly in DI and composition coverage.
- Notes: Current direction is three focused factories: Service Bus, Blob, and Key Vault.

### R003 — In-process execution with `dotnet test`
- Class: primary-user-loop
- Status: validated
- Description: Supported scenarios must run inside the test process with `dotnet test`, without Azure, Docker, or any other external infrastructure.
- Why it matters: Fast, repeatable local integration tests are the whole reason this library exists.
- Source: user
- Primary owning slice: M001/S01
- Supporting slices: M001/S02, M001/S03, M001/S04, M001/S05
- Validation: D030 plus the milestone-closeout `dotnet test ./Azure.InMemory.sln` run (68/68 passed) prove the supported M001 scenarios execute in-process with no Azure, Docker, or other external infrastructure.
- Notes: This is a hard constraint for the project, not a nice-to-have.

### R004 — Real SDK backend behind the same seam
- Class: integration
- Status: validated
- Description: The same factory seam must support official Azure SDK-backed behavior when the project chooses to run against real resources.
- Why it matters: The seam is only credible if it supports both real and in-memory backends.
- Source: user
- Primary owning slice: M001/S01
- Supporting slices: none
- Validation: S01 SDK-backed factories adapt DI-registered official Azure clients behind the same seams, and registration tests prove the SDK-backed seams resolve correctly.
- Notes: M001 proves the registration and adapter shape; full production-grade depth can improve later.

### R005 — Service Bus in-memory send and receive flow
- Class: primary-user-loop
- Status: validated
- Description: A test must be able to publish a Service Bus message into an in-memory topic/subscription or queue path and make it available for processing.
- Why it matters: This is the anchor scenario for proving the library is useful beyond mocks.
- Source: user
- Primary owning slice: M001/S02
- Supporting slices: M001/S03
- Validation: S02 `InMemoryServiceBusIngressTests` prove queues, topics, and subscriptions can be declared in memory and messages become observable on the correct queue or canonical subscription paths.
- Notes: M001 focuses on the basic path, not advanced messaging semantics.

### R006 — Service Bus processor settlement basics
- Class: core-capability
- Status: validated
- Description: The in-memory Service Bus implementation must support processor execution with observable `CompleteMessageAsync` and `DeadLetterMessageAsync` outcomes.
- Why it matters: The user's first end-to-end test depends on these settlement paths being truthful and inspectable.
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: M001/S02
- Validation: S03 processor execution tests prove `CompleteMessageAsync` and `DeadLetterMessageAsync` move envelopes into inspectable completed and dead-lettered outcome stores on declared entity paths.
- Notes: Advanced retries, sessions, and other edge semantics are deferred.

### R007 — Service Bus test observability surface
- Class: failure-visibility
- Status: validated
- Description: Tests must have extra inspection APIs or harness state to assert what happened during in-memory Service Bus processing.
- Why it matters: The official SDK surface alone is not enough for the kinds of assertions this library needs to support.
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: none
- Validation: S03 exposes and tests pending, completed, dead-lettered, and errored inspection surfaces with actionable diagnostics for undeclared topology and invalid settlement ordering.
- Notes: Expected signals include completed, dead-lettered, pending, and errored processing outcomes.

### R008 — Key Vault in-memory read and write
- Class: core-capability
- Status: validated
- Description: A test must be able to write and read secrets through the in-memory Key Vault provider.
- Why it matters: Key Vault is part of the promised MVP and should cover the basic get/set path from the first milestone.
- Source: user
- Primary owning slice: M001/S04
- Supporting slices: M001/S01
- Validation: S04 `InMemoryKeyVaultBehaviorTests` prove `SetSecretAsync` / `GetSecretAsync` through `AddAzureKeyVaultInMemory()` and `IAzureKeyVaultFactory` with no external infrastructure.
- Notes: M001 scope is `SetSecret` and `GetSecret` basics.

### R009 — Blob in-memory read and write
- Class: core-capability
- Status: validated
- Description: A test must be able to write and read blobs through the in-memory Blob provider.
- Why it matters: Blob support is part of the MVP and must be usable from the first milestone.
- Source: user
- Primary owning slice: M001/S05
- Supporting slices: M001/S01
- Validation: S05 `InMemoryBlobBehaviorTests` prove upload/download/exists through `AddAzureBlobInMemory()` and `IAzureBlobFactory`, including preserved `contentType`, overwrite behavior, and cloned snapshots.
- Notes: M001 scope is basic read/write behavior only.

### R010 — Test-only inspection APIs beyond the official SDK surface
- Class: differentiator
- Status: validated
- Description: The in-memory providers may expose additional APIs, state, or helpers specifically for test verification.
- Why it matters: This is how the project becomes more useful than a thin mock wrapper.
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: M001/S04, M001/S05
- Validation: S03, S04, and S05 rely on explicit test-only inspection surfaces (`InMemoryServiceBusState`, `InMemoryKeyVaultState`, `InMemoryBlobState`) beyond the official SDK surface to make outcomes assertable.
- Notes: These APIs are expected for tests and do not need to mimic the official SDK.

### R020 — Advanced Service Bus fidelity
- Class: quality-attribute
- Status: validated
- Description: The library should later support richer Service Bus behavior such as retries, delivery count, and other deeper semantics.
- Why it matters: Higher fidelity will make the library catch more integration bugs once the MVP seam is stable.
- Source: user
- Primary owning slice: M002/S01
- Supporting slices: none
- Validation: Validated by S01 and milestone closeout proof: focused redelivery tests, broader Service Bus regression coverage, a full `dotnet test ./Azure.InMemory.sln` pass, and the producer regression rerun inside `scripts/verify-s03-external-consumer.sh` prove explicit next-run redelivery, `DeliveryCount`/`MaxDeliveryCount` progression, and max-delivery dead-letter behavior on queues and canonical subscription paths.
- Notes: Delivered through the existing deterministic in-process seam without adding background retry loops.

### R022 — NuGet publication readiness
- Class: launchability
- Status: validated
- Description: The library should eventually reach a packaging and quality level suitable for NuGet publication.
- Why it matters: External distribution matters later, but not before the implementation quality is proven.
- Source: user
- Primary owning slice: M002/S02
- Supporting slices: M002/S03
- Validation: Validated by S02-S03 and milestone closeout proof: the library now packs with intentional NuGet metadata and the authoritative packaged `README.md`, direct `.nupkg` inspection confirms the shipped package surface, and the external consumer sample restores and uses the package successfully through a sample-local NuGet flow in `scripts/verify-s03-external-consumer.sh`.
- Notes: M002 proves internal-ready and local-feed consumer readiness; public publication rollout can now be treated as a separate release decision.

## Deferred

### R021 — Azure Functions blob-trigger integration
- Class: integration
- Status: deferred
- Description: The Blob provider may later integrate with Azure Functions-style blob trigger behavior in memory.
- Why it matters: It could extend the usefulness of the library for event-driven storage scenarios.
- Source: user
- Primary owning slice: M002 (provisional)
- Supporting slices: none
- Validation: unmapped
- Notes: Deliberately out of M001 because it drags runtime/host concerns into the MVP.

## Out of Scope

### R030 — Zero-refactor consumer adoption
- Class: constraint
- Status: out-of-scope
- Description: Consumers are not required to adopt the library with zero code changes.
- Why it matters: This prevents the project from turning into an impossible drop-in clone of the Azure SDKs.
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Consumers may need to refactor into the new factory/composition seam.

### R031 — Full Azure SDK surface parity
- Class: anti-feature
- Status: out-of-scope
- Description: The project will not attempt to implement the entire official Azure SDK surface.
- Why it matters: Full parity would explode the scope and bury the useful testing seam.
- Source: inferred
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: The product is a focused mini-framework, not an Azure SDK clone.

### R032 — Docker-based emulator orchestration
- Class: anti-feature
- Status: out-of-scope
- Description: The project will not depend on Docker-based emulators as part of its primary testing loop.
- Why it matters: That would undermine the in-process `dotnet test` value proposition.
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: External emulators may still exist in the ecosystem, but they are not this project's answer.

## Traceability

| ID | Class | Status | Primary owner | Supporting | Proof |
|---|---|---|---|---|---|
| R001 | launchability | validated | M001/S01 | none | S01 registration + mixed composition tests |
| R002 | core-capability | validated | M001/S01 | none | S01 focused factory seams + DI coverage |
| R003 | primary-user-loop | validated | M001/S01 | M001/S02, M001/S03, M001/S04, M001/S05 | D030 + 68/68 milestone closeout run |
| R004 | integration | validated | M001/S01 | none | S01 SDK-backed registration tests |
| R005 | primary-user-loop | validated | M001/S02 | M001/S03 | S02 ingress tests |
| R006 | core-capability | validated | M001/S03 | M001/S02 | S03 processor settlement tests |
| R007 | failure-visibility | validated | M001/S03 | none | S03 outcome inspection coverage |
| R008 | core-capability | validated | M001/S04 | M001/S01 | S04 Key Vault behavior tests |
| R009 | core-capability | validated | M001/S05 | M001/S01 | S05 Blob behavior tests |
| R010 | differentiator | validated | M001/S03 | M001/S04, M001/S05 | Shared-state inspection surfaces |
| R020 | quality-attribute | validated | M002/S01 | none | S01 redelivery tests + milestone closeout regression loop |
| R021 | integration | deferred | M002 (provisional) | none | unmapped |
| R022 | launchability | validated | M002/S02 | M002/S03 | S02 package inspection + S03 external consumer proof |
| R030 | constraint | out-of-scope | none | none | n/a |
| R031 | anti-feature | out-of-scope | none | none | n/a |
| R032 | anti-feature | out-of-scope | none | none | n/a |

## Coverage Summary

- Active requirements: 0
- Mapped to slices: 0
- Validated: 12
- Unmapped active requirements: 0
