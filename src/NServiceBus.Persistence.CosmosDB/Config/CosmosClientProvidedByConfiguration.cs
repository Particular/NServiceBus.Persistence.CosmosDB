namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    class CosmosClientProvidedByConfiguration : IProvideCosmosClient
    {
        public CosmosClient Client { get; set; }
    }
}