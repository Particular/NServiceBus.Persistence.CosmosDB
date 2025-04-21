namespace NServiceBus.Persistence.CosmosDB.Tests;

using Microsoft.Azure.Cosmos;

class FakeCosmosClient(Container fakeContainer) : CosmosClient
{
    public override Container GetContainer(string databaseId, string containerId) => fakeContainer;
}