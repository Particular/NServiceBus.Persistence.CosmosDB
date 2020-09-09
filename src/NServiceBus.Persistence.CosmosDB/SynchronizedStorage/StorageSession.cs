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
            var batchAndMappings = new List<(TransactionalBatchDecorator transactionalBatchDecorator, Dictionary<int, Modification> mappings)>();
            TransactionalBatchDecorator current = null;
            Dictionary<int, Modification> currentMappingDictionary = null;
            var freshBatch = false;
            foreach (var modification in Modifications)
            {
                PartitionKey key;
                PartitionKeyPath path;
                if (PartitionKey.HasValue && partitionKeyPath.HasValue)
                {
                    key = PartitionKey.Value;
                    path = partitionKeyPath.Value;
                }
                else
                {
                    key = modification.PartitionKey;
                    path = modification.PartitionKeyPath;
                    freshBatch = true;
                }

                if (current == null)
                {
                    current = new TransactionalBatchDecorator(Container.CreateTransactionalBatch(key));
                }

                if (currentMappingDictionary == null)
                {
                    currentMappingDictionary = new Dictionary<int, Modification>();
                }

                modification.Apply(current, key, path);
                currentMappingDictionary[current.Index] = modification;

                if (!freshBatch)
                {
                    continue;
                }

                batchAndMappings.Add((current, currentMappingDictionary));
                current = null;
                currentMappingDictionary = null;
            }


            if (current != null)
            {
                batchAndMappings.Add((current, currentMappingDictionary));
            }

            if (batchAndMappings.Count == 0)
            {
                return;
            }

            foreach (var (batch, mappings) in batchAndMappings)
            {
                using(batch)
                using (var batchOutcomeResponse = await batch.Inner.ExecuteAsync().ConfigureAwait(false))
                {
                    for (var i = 0; i < batchOutcomeResponse.Count; i++)
                    {
                        var result = batchOutcomeResponse[i];

                        if (mappings.TryGetValue(i, out var modification))
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
        }

        public void Dispose()
        {
        }

        PartitionKeyPath? partitionKeyPath;
        readonly bool ownsBatch;
    }
}