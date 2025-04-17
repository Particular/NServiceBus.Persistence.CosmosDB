namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

static class TransactionalBatchExtensions
{
    internal static async Task ExecuteOperationAsync(this TransactionalBatch transactionalBatch, IOperation operation, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
    {
        operation.Apply(transactionalBatch, partitionKeyPath);

        using (TransactionalBatchResponse batchOutcomeResponse = await transactionalBatch.ExecuteAsync(cancellationToken).ConfigureAwait(false))
        {
            if (batchOutcomeResponse.Count > 1)
            {
                throw new Exception($"The transactional batch was expected to have a single operation but contained {batchOutcomeResponse.Count} operations.");
            }

            TransactionalBatchOperationResult result = batchOutcomeResponse[0];

            if (result.IsSuccessStatusCode)
            {
                operation.Success(result);
                return;
            }

            // guaranteed to throw
            operation.Conflict(result);
        }
    }

    internal static async Task ExecuteOperationsAsync<TOperation>(this TransactionalBatch transactionalBatch, Dictionary<int, TOperation> operationMappings, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
        where TOperation : IOperation
    {
        foreach (TOperation operation in operationMappings.Values)
        {
            operation.Apply(transactionalBatch, partitionKeyPath);
        }

        using (TransactionalBatchResponse batchOutcomeResponse = await transactionalBatch.ExecuteAsync(cancellationToken).ConfigureAwait(false))
        {
            for (int i = 0; i < batchOutcomeResponse.Count; i++)
            {
                TransactionalBatchOperationResult result = batchOutcomeResponse[i];

                operationMappings.TryGetValue(i, out TOperation operation);
                IOperation operationToBeExecuted = operation ?? ThrowOnConflictOperation.Instance;

                if (result.IsSuccessStatusCode)
                {
                    operationToBeExecuted.Success(result);
                    continue;
                }

                // guaranteed to throw
                operationToBeExecuted.Conflict(result);
            }
        }
    }

    internal static async Task ExecuteAndDisposeOperationsAsync<TOperation>(this TransactionalBatch transactionalBatch, Dictionary<int, TOperation> operationMappings, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
        where TOperation : IOperation
    {
        try
        {
            await transactionalBatch.ExecuteOperationsAsync(operationMappings, partitionKeyPath, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            foreach (TOperation operation in operationMappings.Values)
            {
                operation.Dispose();
            }
        }
    }
}