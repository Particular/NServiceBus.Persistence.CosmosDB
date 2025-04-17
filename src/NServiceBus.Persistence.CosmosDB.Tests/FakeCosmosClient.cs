namespace NServiceBus.Persistence.CosmosDB.Tests;

using Microsoft.Azure.Cosmos;

class FakeCosmosClient : CosmosClient
{
    readonly Container fakeContainer;

    public FakeCosmosClient(Container fakeContainer) => this.fakeContainer = fakeContainer;

    public override Container GetContainer(string databaseId, string containerId) => fakeContainer;
}