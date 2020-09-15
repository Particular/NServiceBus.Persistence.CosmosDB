using System;
using Microsoft.Azure.Cosmos;

namespace NServiceBus.Persistence.CosmosDB
{
    class ThrowIfNoCosmosClientIsProvided : IProvideCosmosClient
    {
        public CosmosClient Client => throw new Exception("No CosmosClient has been configured.");
    }
}
