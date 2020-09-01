namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Pipeline;
    using Transport;

    class StorageSessionFactory : ISynchronizedStorage
    {
        PartitionAwareConfiguration partitionAwareConfiguration;
        CosmosClient cosmosClient;
        string databaseName;

        public StorageSessionFactory(string databaseName, CosmosClient cosmosClient, PartitionAwareConfiguration partitionAwareConfiguration)
        {
            this.databaseName = databaseName;
            this.cosmosClient = cosmosClient;
            this.partitionAwareConfiguration = partitionAwareConfiguration;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            var incomingMessage = contextBag.Get<IncomingMessage>();
            var logicalMessage = contextBag.Get<LogicalMessage>();

            var partitionKey = partitionAwareConfiguration.MapMessageToPartition(incomingMessage.Headers, incomingMessage.MessageId, logicalMessage.MessageType, logicalMessage.Instance);
            var containerName = partitionAwareConfiguration.MapMessageToContainer(logicalMessage.MessageType);
            var container = cosmosClient.GetContainer(databaseName, containerName);

            return Task.FromResult<CompletableSynchronizedStorageSession>(new StorageSession(container, partitionKey));
        }
    }
}