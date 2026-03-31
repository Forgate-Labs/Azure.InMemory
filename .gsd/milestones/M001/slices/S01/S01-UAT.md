# S01: Provider registration and focused factories — UAT

**Milestone:** M001
**Written:** 2026-03-30T21:16:45.638Z

# S01 UAT — Provider registration and focused factories

## Preconditions
- Working directory: `/mnt/c/Eduardo/ForgateLabs/AzureInMemory/Azure.InMemory/.gsd/worktrees/M001`
- .NET 10 SDK is installed.
- No Azure resources, Docker containers, or external emulators are required.

## Test Case 1 — Service Bus SDK registration resolves the focused SDK factory
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests.AddAzureServiceBusSdkResolvesTheSdkFactoryAgainstDiRegisteredAzureClients`.
   - Expected: the test passes.
2. Confirm the test name indicates `IAzureServiceBusFactory` resolved when `ServiceBusClient` and `ServiceBusAdministrationClient` were pre-registered in DI.
   - Expected: the resolved implementation is the SDK-backed Service Bus factory.

## Test Case 2 — Service Bus in-memory registration reuses a shared state root and fails fast on conflicts
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests.AddAzureServiceBusInMemoryResolvesTheInMemoryFactoryAndSharedStateRoot`.
   - Expected: the test passes.
2. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~ServiceBusProviderRegistrationTests.ConflictingRegistrationsFailFastWithActionableMessage`.
   - Expected: the test passes.
3. Review the assertions embodied by the passing tests.
   - Expected: repeated resolutions return the same `InMemoryServiceBusFactory` and `InMemoryServiceBusState`, and mixing `AddAzureServiceBusSdk()` with `AddAzureServiceBusInMemory()` throws an actionable conflict message.
4. Edge check: note the current processor limitation.
   - Expected: S01 does **not** claim working in-memory processing yet; `StartProcessingAsync()` remains intentionally unimplemented for later slices.

## Test Case 3 — Blob seam supports both backend selection and infrastructure-free basic I/O
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~BlobProviderRegistrationTests`.
   - Expected: all Blob provider registration tests pass.
2. Confirm the covered behaviors from the passing suite.
   - Expected: `AddAzureBlobSdk()` resolves the SDK-backed factory when `BlobServiceClient` is in DI; `AddAzureBlobInMemory()` resolves the in-memory factory and shared `InMemoryBlobState`; the in-memory blob client supports upload, exists, and download; conflicting SDK + in-memory Blob registration fails fast.
3. Edge check: verify overwrite behavior remains explicit.
   - Expected: the in-memory implementation is designed around `overwrite: false` by default, so callers must opt in to replacement behavior instead of silently overwriting stored content.

## Test Case 4 — Key Vault seam supports both backend selection and infrastructure-free basic secret flow
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~KeyVaultProviderRegistrationTests`.
   - Expected: all Key Vault provider registration tests pass.
2. Confirm the covered behaviors from the passing suite.
   - Expected: `AddAzureKeyVaultSdk()` resolves the SDK-backed factory when `SecretClient` is in DI; `AddAzureKeyVaultInMemory()` resolves the in-memory factory and shared `InMemoryKeyVaultState`; the in-memory secret store supports set/get with generated versions; missing secrets return `null`; conflicting SDK + in-memory Key Vault registration fails fast.
3. Edge check: confirm the SDK adapter’s missing-secret behavior.
   - Expected: the adapter maps 404 responses to `null` through the focused seam instead of leaking that Azure-specific failure mode upward.

## Test Case 5 — Mixed-resource composition keeps backend choice independent per resource
1. Run `dotnet test ./Azure.InMemory.sln --filter FullyQualifiedName~MixedProviderCompositionTests`.
   - Expected: the mixed composition suite passes.
2. Confirm the first mixed mode scenario exercised by the passing suite.
   - Expected: one service collection can choose Service Bus in-memory, Blob SDK, and Key Vault in-memory, and each resolved factory matches its selected backend.
3. Confirm the second mixed mode scenario exercised by the passing suite.
   - Expected: one service collection can choose Service Bus SDK, Blob in-memory, and Key Vault SDK, and each resolved factory matches its selected backend.
4. Confirm the conflict isolation checks exercised by the passing suite.
   - Expected: changing Blob or Key Vault backends does not weaken the Service Bus conflict guard, and the equivalent statement holds for Blob and Key Vault.

## Final Slice Proof
1. Run `dotnet test ./Azure.InMemory.sln`.
   - Expected: the full suite passes with no external infrastructure.
2. Confirm the overall result.
   - Expected: 20 tests pass, covering focused factory resolution, shared state reuse, actionable missing-client diagnostics, same-resource conflict handling, truthful Blob/Key Vault basics, and mixed-resource composition.

## Acceptance Notes
- This slice is accepted when all commands above pass locally.
- If the full suite fails only because Service Bus processing is not implemented, that is **not** an S01 regression unless a test added by S01 claimed processing support. The accepted S01 contract is provider registration plus focused factories, with Service Bus processing reserved for later slices.
