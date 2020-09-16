namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    public interface ICosmosDBStorageSession
    {
        /// <summary>
        ///
        /// </summary>
        TransactionalBatch Batch { get; }
    }
}