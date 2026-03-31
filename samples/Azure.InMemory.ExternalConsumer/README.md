# Forgate.Azure.InMemory.ExternalConsumer

This sample is the committed package-only consumer boundary for slice S03.

## Single-command verification

Run the full producer-to-package-to-consumer proof with one command:

```bash
DOTNET_CLI_UI_LANGUAGE=en bash ./scripts/verify-s03-external-consumer.sh
```

The script is the authoritative verification entrypoint for this slice. It fails fast and leaves the failing stage obvious in terminal output:

1. packs `src/Azure.InMemory/Azure.InMemory.csproj` into `./artifacts/pack`;
2. recreates `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages` so restore state stays isolated;
3. restores the external consumer with its sample-local `NuGet.Config`, `--packages`, and `--no-cache`;
4. runs the focused `ExternalConsumerQueueRedeliveryTests` consumer proof with `--no-restore`;
5. reruns `dotnet test ./Azure.InMemory.sln` as the producer regression guard.

If you need to inspect or troubleshoot a specific stage, read `scripts/verify-s03-external-consumer.sh` directly and then compare the expected restore settings below.

## Guardrails

- Keep this project **out of** `Azure.InMemory.sln`.
- Keep `Forgate.Azure.InMemory` referenced **only** through `<PackageReference Include="Forgate.Azure.InMemory" Version="1.0.0" />`.
- Do **not** add a `ProjectReference` back to `src/Azure.InMemory/Azure.InMemory.csproj`.
- Keep `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` in the sample `.csproj` so repo-root CPM does not mask restore problems.

## Deterministic restore

Use the repo-local NuGet config and a dedicated package cache so restore proof does not depend on machine-wide sources or stale packages:

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet restore ./samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj \
  --configfile ./samples/Azure.InMemory.ExternalConsumer/NuGet.Config \
  --packages ./samples/Azure.InMemory.ExternalConsumer/.nuget/packages \
  --no-cache
```

`NuGet.Config` clears inherited package sources and points explicitly at:

- `../../artifacts/pack` for the repo-local `Forgate.Azure.InMemory.1.0.0.nupkg`
- `https://api.nuget.org/v3/index.json` for public test dependencies

The dedicated `./samples/Azure.InMemory.ExternalConsumer/.nuget/packages` folder is part of the proof: it keeps restore results isolated from shared cache state and makes feed/config problems surface directly.

## Meaningful package proof

`ExternalConsumerQueueRedeliveryTests.cs` is the committed proof that this harness uses the packaged README seam instead of producer-only helpers.

It intentionally:

- builds a fresh `ServiceProvider` per test and registers the package with `AddAzureServiceBusInMemory()`;
- resolves `IAzureServiceBusFactory` to declare queue topology, create the sender, and create the processor exactly the way the package README shows;
- uses `InMemoryServiceBusState` only as the package's public test-inspection surface so the assertions stay at the consumer boundary;
- fails the first `StartProcessingAsync()` run on purpose, then asserts one pending message and one errored outcome with `DeliveryCount == 2` visible after that first run;
- calls `StartProcessingAsync()` a second time and proves that explicit rerun is the moment the message completes.

The suite also includes negative checks for undeclared topology and wrong-queue processing so seam drift or package transitive issues fail as direct xUnit assertions instead of slipping through as a compile-only smoke test.
