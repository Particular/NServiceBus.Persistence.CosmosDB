namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    // Required for testing
    interface ITransactionalBatchProvider
    {
        TransactionalBatch TransactionalBatch { get; }
    }
}