namespace NServiceBus.Persistence.CosmosDB.Tests;

using Microsoft.Azure.Cosmos;

class FakeProvider : IProvideCosmosClient
{
    public FakeProvider(CosmosClient fakeClient) => Client = fakeClient;
    public CosmosClient Client { get; }
}