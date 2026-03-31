# M002: 

## Vision
Deepen trust in Azure.InMemory by shipping one narrow but realistic Service Bus fidelity upgrade through the existing explicit seam, then prove the library can be packed and consumed as an internal-ready package from outside the producer project boundary.

## Slice Overview
| ID | Slice | Risk | Depends | Done | After this |
|----|-------|------|---------|------|------------|
| S01 | Observable Service Bus redelivery fidelity | high | — | ✅ | A focused Service Bus scenario proves that an unsuccessfully processed message is redelivered with incremented delivery count and automatically dead-lettered after the configured maximum, with all evidence visible through the existing in-memory test harness and no seam rewrite. |
| S02 | Internal-ready package surface and docs | medium | S01 | ✅ | `dotnet pack` emits an internal-ready Azure.InMemory package, and package-facing docs/examples show another team how to install the package and wire the in-memory Service Bus provider without guessing hidden setup. |
| S03 | External consumer package proof | medium | S01, S02 | ✅ | A fresh consumer project restores Azure.InMemory from the packed artifact through a local NuGet flow, follows the docs to register `AddAzureServiceBusInMemory()`, and passes a meaningful local `dotnet test` scenario using the packaged library. |
