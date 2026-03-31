# M001: 

## Vision
Build a .NET test library that behaves like an Azure-focused version of EF Core InMemory, using explicit resource-specific registration and focused factories so test projects can switch between SDK-backed and in-memory behavior for Service Bus, Blob, and Key Vault.

## Slice Overview
| ID | Slice | Risk | Depends | Done | After this |
|----|-------|------|---------|------|------------|
| S01 | Provider registration and focused factories | high | — | ✅ | A test host can choose `AddAzureServiceBusSdk()` or `AddAzureServiceBusInMemory()` and resolve the focused factory for the selected mode; the same pattern exists for Blob and Key Vault. |
| S02 | In-memory Service Bus topology and message ingress | high | S01 | ✅ | A test creates a topic and subscription in memory, publishes a message, and can observe that the message is available to the in-memory Service Bus pipeline. |
| S03 | Processor execution and settlement observability | high | S02 | ✅ | An in-memory processor consumes a published message and the test can assert whether it was completed, dead-lettered, left pending, or errored. |
| S04 | In-memory Key Vault basics | medium | S01 | ✅ | A test writes a secret and reads it back through the configured Key Vault factory with no external infrastructure. |
| S05 | In-memory Blob basics | medium | S01 | ✅ | A test writes a blob and reads it back through the configured Blob factory with no Azure account or Docker. |
