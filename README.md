# NServiceBus.Persistence.CosmosDB

CosmosDB Persistence for Microsoft CosmosDB Core API

## Running tests locally

Test projects included in the solution rely on the environment variable `CosmosDBPersistence_ConnectionString` and `AzureStoragePersistence_ConnectionString` for Cosmos DB and Table Storage. Absense of these environment variables will fallback to using Cosmos DB and Storage local emulator.
