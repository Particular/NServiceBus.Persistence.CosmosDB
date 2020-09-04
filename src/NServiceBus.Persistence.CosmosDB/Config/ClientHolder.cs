namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    // otherwise the client gets disposed
    class ClientHolder
    {
        public CosmosClient Client { get; set; }
    }
}