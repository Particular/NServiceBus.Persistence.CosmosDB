namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    static class TransactionalBatchExtensions
    {
        internal static async Task ExecuteOperationAsync(this TransactionalBatch transactionalBatch, Operation operation, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
        {
            operation.Apply(transactionalBatch, partitionKeyPath);

            using (var batchOutcomeResponse = await transactionalBatch.ExecuteAsync(cancellationToken).ConfigureAwait(false))
            {
                if (batchOutcomeResponse.Count > 1)
                {
                    throw new Exception($"The transactional batch was expected to have a single operation but contained {batchOutcomeResponse.Count} operations.");
                }

                var result = batchOutcomeResponse[0];

                if (result.IsSuccessStatusCode)
                {
                    operation.Success(result);
                    return;
                }

                // guaranteed to throw
                operation.Conflict(result);
            }
        }

        internal static async Task ExecuteOperationsAsync(this TransactionalBatch transactionalBatch, Dictionary<int, Operation> operationMappings, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
        {
            foreach (var operation in operationMappings.Values)
            {
                operation.Apply(transactionalBatch, partitionKeyPath);
            }

            using (var batchOutcomeResponse = await transactionalBatch.ExecuteAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    operationMappings.TryGetValue(i, out var operation);
                    operation ??= ThrowOnConflictOperation.Instance;

                    if (result.IsSuccessStatusCode)
                    {
                        operation.Success(result);
                        continue;
                    }

                    // guaranteed to throw
                    operation.Conflict(result);
                }
            }
        }
    }
}