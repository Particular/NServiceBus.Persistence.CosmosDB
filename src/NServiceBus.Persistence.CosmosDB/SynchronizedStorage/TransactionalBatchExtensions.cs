namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Azure.Cosmos;

    static class TransactionalBatchExtensions
    {
        internal static async Task ExecuteOperationAsync(this TransactionalBatch transactionalBatch, IOperation operation, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
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

        internal static async Task ExecuteOperationsAsync<TOperation>(this TransactionalBatch transactionalBatch, Dictionary<int, TOperation> operationMappings, PartitionKeyPath partitionKeyPath, CancellationToken cancellationToken = default)
            where TOperation : IOperation
        {
            foreach (var operation in operationMappings.Values)
            {
                operation.Apply(transactionalBatch, partitionKeyPath);
            }

            using (var batchOutcomeResponse = await transactionalBatch.ExecuteAsync(cancellationToken).ConfigureAwait(false))
            {
                log.Info($"CosmosDB:TransactionalBatchExtensions:ExecuteOperationsAsync, ActivityId: {batchOutcomeResponse?.ActivityId}, Count: {batchOutcomeResponse?.Count}, RequestCharge: {batchOutcomeResponse?.RequestCharge}");

                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    operationMappings.TryGetValue(i, out var operation);
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
                foreach (var operation in operationMappings.Values)
                {
                    operation.Dispose();
                }
            }
        }

        internal static ILog log = LogManager.GetLogger<TransactionalBatch>();
    }
}