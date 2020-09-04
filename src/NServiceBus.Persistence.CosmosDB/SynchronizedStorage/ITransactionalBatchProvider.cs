namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    interface ITransactionalBatchProvider
    {
        TransactionalBatch TransactionalBatch { get; }
    }
}