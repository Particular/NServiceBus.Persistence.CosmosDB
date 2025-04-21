namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;

class StorageSession(ContainerHolderResolver resolver, ContextBag context) : IWorkWithSharedTransactionalBatch
{
    public void AddOperation(IOperation operation)
    {
        PartitionKey operationPartitionKey = operation.PartitionKey;

        if (operation is IReleaseLockOperation cleanupOperation)
        {
            releaseLockOperations ??= [];
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
            operations.Add(operationPartitionKey, []);
        }

        int index = operations[operationPartitionKey].Count;
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

        foreach (KeyValuePair<PartitionKey, Dictionary<int, IOperation>> batchOfOperations in operations)
        {
            TransactionalBatch transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfOperations.Key);

            await transactionalBatch.ExecuteOperationsAsync(batchOfOperations.Value, ContainerHolder.PartitionKeyPath, cancellationToken).ConfigureAwait(false);
        }

        // when we successfully executed all operations we know we don't have to execute any release operations, so we dispose if necessary and clear them out
        foreach (KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>> batchOfReleaseLockOperations in releaseLockOperations ?? Enumerable.Empty<KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>>>())
        {
            foreach (IReleaseLockOperation operation in batchOfReleaseLockOperations.Value.Values)
            {
                operation.Dispose();
            }
        }

        releaseLockOperations = null;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<PartitionKey, Dictionary<int, IOperation>> batchOfOperations in operations)
        {
            foreach (IOperation operation in batchOfOperations.Value.Values)
            {
                operation.Dispose();
            }
        }

        // The persistence tests to Get requests within a synchronized storage session scope that is completed at the end. Since these get requests never add
        // any operations there is nothing to commit (operations.Count == 0) and the release operations will not be cleaned making sure the acquired lock will be freed to not block
        // other get requests and slow down tests.
        foreach (KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>> batchOfReleaseLockOperations in releaseLockOperations ?? Enumerable.Empty<KeyValuePair<PartitionKey, Dictionary<int, IReleaseLockOperation>>>())
        {
            TransactionalBatch transactionalBatch = ContainerHolder.Container.CreateTransactionalBatch(batchOfReleaseLockOperations.Key);

            // We are optimistic and fire-and-forget the releasing of the lock and just continue. In case this fails the next message that needs to acquire the lock wil have to wait.
            _ = transactionalBatch.ExecuteAndDisposeOperationsAsync(batchOfReleaseLockOperations.Value, ContainerHolder.PartitionKeyPath, CancellationToken.None);
        }
    }

    public ContextBag CurrentContextBag { get; set; } = context;
    public Container Container => ContainerHolder.Container;
    public PartitionKeyPath PartitionKeyPath => ContainerHolder.PartitionKeyPath;
    public ContainerHolder ContainerHolder { get; set; } = resolver.ResolveAndSetIfAvailable(context);

    readonly Dictionary<PartitionKey, Dictionary<int, IOperation>> operations = [];
    Dictionary<PartitionKey, Dictionary<int, IReleaseLockOperation>> releaseLockOperations;
}