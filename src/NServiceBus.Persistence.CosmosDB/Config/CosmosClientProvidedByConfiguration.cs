using Microsoft.Azure.Cosmos;

namespace NServiceBus.Persistence.CosmosDB
{
    class CosmosClientProvidedByConfiguration : IProvideCosmosClient
    {
        public CosmosClient Client { get; set; }
    }
}
