namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Outbox;

    class CosmosOutboxTransaction : OutboxTransaction
    {
        public StorageSession StorageSession { get; }
        public PartitionKey? PartitionKey { get; set; }

        public bool SuppressStoreAndCommit { get; set; } = false;

        public CosmosOutboxTransaction(Container container)
        {
            StorageSession = new StorageSession(container, false);
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