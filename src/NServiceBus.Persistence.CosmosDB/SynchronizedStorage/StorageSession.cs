namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;

    class StorageSession : ICompletableSynchronizedStorageSession, IWorkWithSharedTransactionalBatch
    {
        // When outbox is involved, commitOnComplete will be false
        public StorageSession(ContainerHolderResolver resolver, ContextBag context, bool commitOnComplete)
        {
            this.commitOnComplete = commitOnComplete;
            CurrentContextBag = context;
            ContainerHolder = resolver.ResolveAndSetIfAvailable(context);
        }

        Task ICompletableSynchronizedStorageSession.CompleteAsync(CancellationToken cancellationToken) =>
            commitOnComplete ? Commit(cancellationToken) : Task.CompletedTask;

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

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            // in case there is nothing to do don't even bother checking the rest
            if (operations.Count == 0)
            {
                return;
            }

            if (ContainerHolder == null)
            {
                throw new Exception("Unable to retrieve the container name and the partition key during processing. Make sure that either `persistence.Container()` is used or the relevant container information is available on the message handling pipeline.");
            }

            foreach (var batchOfOperations in operations)
            {
                var transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfOperations.Key);

                await transactionalBatch.ExecuteOperationsAsync(batchOfOperations.Value, ContainerHolder.PartitionKeyPath, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        public Container Container => ContainerHolder.Container;
        public PartitionKeyPath PartitionKeyPath => ContainerHolder.PartitionKeyPath;
        public ContainerHolder ContainerHolder { get; set; }

        readonly Dictionary<PartitionKey, Dictionary<int, Operation>> operations = new Dictionary<PartitionKey, Dictionary<int, Operation>>();
    }
}