namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Outbox;

    class CosmosOutboxTransaction : OutboxTransaction
    {
        public StorageSession StorageSession { get; }
        public PartitionKey? PartitionKey { get; set; }

        // By default, store and commit are enabled
        public bool SuppressStoreAndCommit { get; set; }

        public CosmosOutboxTransaction(ContainerHolder containerHolder, ContextBag context)
        {
            StorageSession = new StorageSession(containerHolder, context, false);
        }

        public Task Commit()
        {
            if (SuppressStoreAndCommit)
            {
                return Task.CompletedTask;
            }

            return StorageSession.Commit();
        }

        public void Dispose()
        {
            StorageSession.Dispose();
        }
    }
}