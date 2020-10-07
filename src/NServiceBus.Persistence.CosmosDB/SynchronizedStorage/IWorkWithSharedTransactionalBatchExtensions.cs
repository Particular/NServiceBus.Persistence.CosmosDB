namespace NServiceBus
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    static class IWorkWithSharedTransactionalBatchExtensions
    {
        public static ICosmosStorageSession Create(this IWorkWithSharedTransactionalBatch workWith)
        {
            if (!workWith.CurrentContextBag.TryGet<PartitionKey>(out var partitionKey))
            {
                throw new Exception("To use the shared transactional batch a partition key must be set using a custom pipeline behavior.");
            }
            return new SharedTransactionalBatch(workWith, partitionKey);
        }
    }
}