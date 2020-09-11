namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    static class TransactionalBatchDecoratorExtensions
    {
        internal static async Task Execute(this TransactionalBatchDecorator transactionalBatch, Dictionary<int, Operation> operationMappings)
        {
            using (var batchOutcomeResponse = await transactionalBatch.Inner.ExecuteAsync().ConfigureAwait(false))
            {
                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    if (operationMappings.TryGetValue(i, out var modification))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            modification.Success(result);
                            continue;
                        }

                        if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            // guaranteed to throw
                            modification.Conflict(result);
                        }
                    }

                    if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        throw new Exception("Concurrency conflict.");
                    }

                    if (result.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new Exception("Bad request. Quite likely the partition key did not match");
                    }
                }
            }
        }
    }
}