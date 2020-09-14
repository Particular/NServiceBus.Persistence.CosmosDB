﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    static class TransactionalBatchDecoratorExtensions
    {
        internal static async Task Execute(this TransactionalBatchDecorator transactionalBatch, Operation operation)
        {
            operation.Apply(transactionalBatch);

            using (var batchOutcomeResponse = await transactionalBatch.Inner.ExecuteAsync().ConfigureAwait(false))
            {
                if (batchOutcomeResponse.Count > 1)
                {
                    throw new Exception("The transactional batch was intended to be used with a single operation but contained more than one.");
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

        internal static async Task Execute(this TransactionalBatchDecorator transactionalBatch, Dictionary<int, Operation> operationMappings)
        {
            foreach (var operation in operationMappings.Values)
            {
                operation.Apply(transactionalBatch);
            }

            using (var batchOutcomeResponse = await transactionalBatch.Inner.ExecuteAsync().ConfigureAwait(false))
            {
                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    operationMappings.TryGetValue(i, out var operation);
                    operation = operation ?? ThrowOnConflictOperation.Instance;

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