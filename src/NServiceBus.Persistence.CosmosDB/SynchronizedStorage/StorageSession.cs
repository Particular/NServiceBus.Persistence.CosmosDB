namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    class StorageSession : CompletableSynchronizedStorageSession
    {
        // When outbox is involved, commitOnComplete will be false
        public StorageSession(Container container, bool commitOnComplete)
        {
            Container = container;
            this.commitOnComplete = commitOnComplete;
        }

        Task CompletableSynchronizedStorageSession.CompleteAsync()
        {
            return commitOnComplete ? Commit() : Task.CompletedTask;
        }

        void IDisposable.Dispose()
        {
            if (!commitOnComplete)
            {
                return;
            }

            Dispose();
        }

        // TODO: is this enough to be thread safe or do we need to worry about concurrent access?
        public void AddOperation(Operation operation)
        {
            var operationPartitionKey = operation.PartitionKey;

            if (!operations.ContainsKey(operationPartitionKey))
            {
                operations.Add(operationPartitionKey, new Dictionary<int, Operation>());
            }

            var index = operations[operationPartitionKey].Count;
            operations[operationPartitionKey].Add(index, operation);
        }

        public async Task Commit()
        {
            foreach (var batchOfOperations in operations)
            {
                using (var transactionalBatch = new TransactionalBatchDecorator(Container.CreateTransactionalBatch(batchOfOperations.Key)))
                {
                    var batchedOperations = batchOfOperations.Value.Values;

                    foreach (var operation in batchedOperations)
                    {
                        operation.Apply(transactionalBatch);
                    }

                    using (var batchOutcomeResponse = await transactionalBatch.Inner.ExecuteAsync().ConfigureAwait(false))
                    {
                        for (var i = 0; i < batchOutcomeResponse.Count; i++)
                        {
                            var result = batchOutcomeResponse[i];

                            if (batchOfOperations.Value.TryGetValue(i, out var modification))
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

                            if (result.StatusCode == HttpStatusCode.BadRequest)
                            {
                                throw new Exception("Bad request. Quite likely the partition key did not match");
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            operations.Clear();
        }

        readonly bool commitOnComplete;
        public Container Container { get; }

        readonly Dictionary<PartitionKey, Dictionary<int, Operation>> operations = new Dictionary<PartitionKey, Dictionary<int, Operation>>();

    }
}