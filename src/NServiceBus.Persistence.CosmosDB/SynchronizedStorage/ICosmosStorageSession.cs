namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    public interface ICosmosStorageSession
    {
        /// <summary>
        /// The partition key
        /// </summary>
        PartitionKey PartitionKey { get; }

        /// <summary>
        /// The partition key path
        /// </summary>
        PartitionKeyPath PartitionKeyPath { get; }

        /// <summary>
        /// The container
        /// </summary>
        Microsoft.Azure.Cosmos.Container Container { get; }

        /// <summary>
        /// The transactional batch
        /// </summary>
        TransactionalBatch Batch { get; }
    }
}