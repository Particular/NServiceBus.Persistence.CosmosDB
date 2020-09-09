namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    class StorageSession : CompletableSynchronizedStorageSession
    {
        public StorageSession(Container container, PartitionKey? partitionKey, PartitionKeyPath? partitionKeyPath, bool ownsBatch)
        {
            this.ownsBatch = ownsBatch;
            this.partitionKeyPath = partitionKeyPath;
            Container = container;
            PartitionKey = partitionKey;
        }

        public Container Container { get; }

        public TransactionalBatch TransactionalBatch
        {
            get
            {
                if (transactionalBatchDecorator == null)
                {
                    transactionalBatchDecorator = new TransactionalBatchDecorator(Container.CreateTransactionalBatch(PartitionKey.Value));
                }

                return transactionalBatchDecorator;
            }
        }

        public PartitionKey? PartitionKey { get; }

        public List<Modification> Modifications { get; } = new List<Modification>();

        Task CompletableSynchronizedStorageSession.CompleteAsync()
        {
            return ownsBatch ? Commit() : Task.CompletedTask;
        }

        void IDisposable.Dispose()
        {
            if (!ownsBatch)
            {
                return;
            }

            Dispose();
        }

        public async Task Commit()
        {
            var mappingDictionary = new Dictionary<int, Modification>();
            foreach (var modification in Modifications)
            {
                modification.Apply(transactionalBatchDecorator, PartitionKey.Value, partitionKeyPath.Value);
                // TODO figure out something
                mappingDictionary[transactionalBatchDecorator.Index] = modification;
            }

            if (transactionalBatchDecorator == null || !transactionalBatchDecorator.CanBeExecuted)
            {
                return;
            }

            using (var batchOutcomeResponse = await transactionalBatchDecorator.Inner.ExecuteAsync().ConfigureAwait(false))
            {
                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    if (mappingDictionary.TryGetValue(i, out var modification))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            modification.Success(result);
                            continue;
                        }

                        if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            // guaranteed to throw
                            modification.Conflict(result);
                        }
                    }

                    if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        throw new Exception("Concurrency conflict.");
                    }

                    if(result.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new Exception("Bad request. Quite likely the partition key did not match");
                    }
                }
            }
        }

        public void Dispose()
        {
            transactionalBatchDecorator?.Dispose();
        }

        TransactionalBatchDecorator transactionalBatchDecorator;
        PartitionKeyPath? partitionKeyPath;
        readonly bool ownsBatch;
    }
}