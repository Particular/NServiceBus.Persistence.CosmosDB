namespace NServiceBus.Persistence.CosmosDB.Tests;

using Microsoft.Azure.Cosmos;

class FakeProvider(CosmosClient fakeClient) : IProvideCosmosClient
{
    public CosmosClient Client { get; } = fakeClient;
}