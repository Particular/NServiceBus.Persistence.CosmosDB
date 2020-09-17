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
        public StorageSession(ContextBag context, bool commitOnComplete)
        {
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

                await transactionalBatch.ExecuteOperationsAsync(batchOfOperations.Value, ContainerHolder.PartitionKeyPath).ConfigureAwait(false);
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
        public ContextBag CurrentContextBag { get; set; }

        public ContainerHolder ContainerHolder
        {
            get
            {
                if (!CurrentContextBag.TryGet<ContainerHolder>(out var containerHolder))
                {
                    // probably throw here?
                }

                return containerHolder;
            }
        }

        readonly Dictionary<PartitionKey, Dictionary<int, Operation>> operations = new Dictionary<PartitionKey, Dictionary<int, Operation>>();
    }
}