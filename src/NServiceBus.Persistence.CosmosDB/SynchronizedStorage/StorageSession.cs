namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    class StorageSession : CompletableSynchronizedStorageSession
    {
        public Container Container { get; }
        public TransactionalBatch TransactionalBatch { get; }

        public StorageSession(Container container, TransactionalBatch transactionalBatch)
        {
            Container = container;
            TransactionalBatch = transactionalBatch;
        }

        public void Dispose()
        {
            // for now
        }

        public async Task CompleteAsync()
        {
            using (var batchOutcomeResponse = await TransactionalBatch.ExecuteAsync().ConfigureAwait(false))
            {
                if (!batchOutcomeResponse.IsSuccessStatusCode)
                {
                    // TODO
                }
            }
        }
    }
}