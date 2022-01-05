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

            if (operation is ICleanupOperation cleanupOperation)
            {
                cleanupOperations ??= new Dictionary<PartitionKey, Dictionary<int, ICleanupOperation>>();
                AddOperation(cleanupOperation, operationPartitionKey, cleanupOperations);
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
        }

        public void Dispose()
        {
            /*
             * This is an ugly hack. The persistence tests do Saga Get requests wrapped with a storage session like the core behaves which is good.
             * Normally though a get request would always be accompanied with a Save, Update Or Delete before completing the session.
             * In order to execute cleanup operations for those Get request too we continue the assumption that Get request will never be lazy
             * executed with operations and always go directly against the container. Hence we treat those Get request from the cleanup perspective as not
             * successful requests and still execute the cleanup behavior that are marked with ExecutesOnFailure = true
             */
            var successful = operations.Count > 0;
            foreach (var batchOfOperations in operations)
            {
                foreach (var operation in batchOfOperations.Value.Values)
                {
                    if (successful)
                    {
                        successful = operation.Successful;
                    }
                    operation.Dispose();
                }
            }

            foreach (var batchOfOperations in cleanupOperations ?? Enumerable.Empty<KeyValuePair<PartitionKey, Dictionary<int, ICleanupOperation>>>())
            {
                Dictionary<int, ICleanupOperation> operationMappings = batchOfOperations.Value;
                List<int> indexesToRemove = null;
                foreach (var operation in operationMappings)
                {
                    if (successful && operation.Value.ExecutesOnFailure)
                    {
                        indexesToRemove ??= new List<int>(operationMappings.Count);
                        indexesToRemove.Add(operation.Key);
                    }
                }

                foreach (var indexToRemove in indexesToRemove ?? Enumerable.Empty<int>())
                {
                    operationMappings.Remove(indexToRemove);
                }

                if (operationMappings.Count == 0)
                {
                    continue;
                }
                var transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfOperations.Key);

                // We are optimistic and fire-and-forget the releasing of the lock and just continue. In case this fails the next message that needs to acquire the lock wil have to wait.
                _ = transactionalBatch.ExecuteAndDisposeOperationsAsync(operationMappings, ContainerHolder.PartitionKeyPath, CancellationToken.None);
            }
        }

        readonly bool commitOnComplete;
        public ContextBag CurrentContextBag { get; set; }
        public Container Container => ContainerHolder.Container;
        public PartitionKeyPath PartitionKeyPath => ContainerHolder.PartitionKeyPath;
        public ContainerHolder ContainerHolder { get; set; }

        readonly Dictionary<PartitionKey, Dictionary<int, IOperation>> operations = new Dictionary<PartitionKey, Dictionary<int, IOperation>>();
        Dictionary<PartitionKey, Dictionary<int, ICleanupOperation>> cleanupOperations;
    }
}