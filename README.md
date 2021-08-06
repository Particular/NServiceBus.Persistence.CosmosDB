# NServiceBus.Persistence.CosmosDB

NServiceBus persistence for [Azure Cosmos DB](https://azure.microsoft.com/en-us/services/cosmos-db/) utilizing the Core (SQL) API.

Documentation, including usage and samples, is available on the [Particular docs site](https://docs.particular.net/persistence/cosmosdb/).

## Running tests locally

All test projects utilize NUnit. The test projects can be executed using the test runner included in Visual Studio or using the [`dotnet test` command](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test) from the command line.

The tests in the AcceptanceTesting projects and the PersistenceTests project require a Cosmos DB server in order for the test to pass.

### Using the Cosmos DB Emulator

The AcceptanceTests and PersistenceTests projects will connect to a [local Cosmos DB emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator?tabs=cli%2Cssl-netstd21) without configuring a connection string.

The Cosmos DB Emulator, including a data explorer, can be located at https://localhost:8081/_explorer/index.html.

Once the emulator is setup, create a Database named `CosmosDBPersistence`.

### Using the Cosmos DB Service

To create a Cosmos DB Core (SQL) Account refer to the [Microsoft instructions](https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-manage-database-account) for managing Accounts.

Once a Cosmos DB account is setup, you can use the [Azure Cosmos explorer](https://docs.microsoft.com/en-us/azure/cosmos-db/data-explorer) to create a Database named `CosmosDBPersistence` which is required by the test projects.

To use the created Cosmos DB Account, set an environment variable named `CosmosDBPersistence_ConnectionString` with [a Cosmos DB connection string](https://docs.microsoft.com/en-us/azure/cosmos-db/secure-access-to-data) for your Account.
