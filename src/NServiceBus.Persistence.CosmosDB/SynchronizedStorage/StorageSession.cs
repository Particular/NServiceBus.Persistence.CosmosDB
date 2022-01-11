namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        public void AddOperation(IOperation operation)
        {
            var operationPartitionKey = operation.PartitionKey;

            if (operation is IReleaseLockOperation cleanupOperation)
            {
                releaseLockOperations ??= new Dictionary<PartitionKey, Dictionary<int, IReleaseLockOperation>>();
                AddOperation(cleanupOperation, operationPartitionKey, releaseLockOperations);
                return;
            }

            AddOperation(operation, operationPartitionKey, operations);
        }

        static void AddOperation<TOperation>(TOperation operation, PartitionKey operationPartitionKey, Dictionary<PartitionKey, Dictionary<int, TOperation>> operations)
            where TOperation : IOperation
        {
            if (!operations.ContainsKey(operationPartitionKey))
            {
                operations.Add(operationPartitionKey, new Dictionary<int, TOperation>());
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

            // when we successfully executed all operations we know we don't have to execute any release operations, so we dispose if necessary and clear them out
            foreach (var batchOfReleaseLockOperations in releaseLockOperations ?? Enumerable.Empty<KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>>>())
            {
                foreach (var operation in batchOfReleaseLockOperations.Value.Values)
                {
                    operation.Dispose();
                }
            }
            releaseLockOperations = null;
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

            // The persistence tests to Get requests within a synchronized storage session scope that is completed at the end. Since these get requests never add
            // any operations there is nothing to commit (operations.Count == 0) and the release operations will not be cleaned making sure the acquired lock will be freed to not block
            // other get requests and slow down tests.
            foreach (var batchOfReleaseLockOperations in releaseLockOperations ?? Enumerable.Empty<KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>>>())
            {
                var transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfReleaseLockOperations.Key);

                // We are optimistic and fire-and-forget the releasing of the lock and just continue. In case this fails the next message that needs to acquire the lock wil have to wait.
                _ = transactionalBatch.ExecuteAndDisposeOperationsAsync(batchOfReleaseLockOperations.Value, ContainerHolder.PartitionKeyPath, CancellationToken.None);
            }
        }

        readonly bool commitOnComplete;
        public ContextBag CurrentContextBag { get; set; }
        public Container Container => ContainerHolder.Container;
        public PartitionKeyPath PartitionKeyPath => ContainerHolder.PartitionKeyPath;
        public ContainerHolder ContainerHolder { get; set; }

        readonly Dictionary<PartitionKey, Dictionary<int, IOperation>> operations = new Dictionary<PartitionKey, Dictionary<int, IOperation>>();
        Dictionary<PartitionKey, Dictionary<int, IReleaseLockOperation>> releaseLockOperations;
    }
}