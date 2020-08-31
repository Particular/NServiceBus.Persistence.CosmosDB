namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Net;
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
                    foreach (var result in batchOutcomeResponse)
                    {
                        if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            // technically would could somehow map back to what we wrote if we store extra info in the session
                            throw new Exception("Concurrent updates lead to write conflicts.");
                        }
                    }
                }
            }
        }
    }
}