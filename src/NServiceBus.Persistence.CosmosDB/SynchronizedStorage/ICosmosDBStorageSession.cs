namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    interface ICosmosDBStorageSession
    {
        TransactionalBatch Batch { get; }
    }
}