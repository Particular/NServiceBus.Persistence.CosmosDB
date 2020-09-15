namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Extensibility;

    class StorageSession : CompletableSynchronizedStorageSession, IWorkWithSharedTransactionalBatch
    {
        // When outbox is involved, commitOnComplete will be false
        public StorageSession(ContainerHolder containerHolder, ContextBag context, bool commitOnComplete)
        {
            ContainerHolder = containerHolder;
            this.commitOnComplete = commitOnComplete;
            CurrentContextBag = context;
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
                var transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfOperations.Key);

                await transactionalBatch.ExecuteOperationsAsync(batchOfOperations.Value).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            foreach (var batchOfOperations in operations)
            {
                foreach (var operation in batchOfOperations.Value.Values)
                {
                    operation.Dispose();
                }
            }

            operations.Clear();
        }

        readonly bool commitOnComplete;
        public ContainerHolder ContainerHolder { get; }
        public ContextBag CurrentContextBag { get; set; }

        readonly Dictionary<PartitionKey, Dictionary<int, Operation>> operations = new Dictionary<PartitionKey, Dictionary<int, Operation>>();
    }
}