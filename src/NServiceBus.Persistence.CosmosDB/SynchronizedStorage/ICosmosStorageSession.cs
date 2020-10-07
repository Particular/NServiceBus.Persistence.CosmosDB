namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Exposes the current <see cref="TransactionalBatch"/> as well the partition key, path and the container that
    /// are managed by NServiceBus.
    /// </summary>
    public interface ICosmosStorageSession
    {
        /// <summary>
        /// The partition key under which all batched items will be stored.
        /// </summary>
        PartitionKey PartitionKey { get; }

        /// <summary>
        /// The partition key path matching the container in use.
        /// </summary>
        PartitionKeyPath PartitionKeyPath { get; }

        /// <summary>
        /// The container that will be used to store the batched items.
        /// </summary>
        Microsoft.Azure.Cosmos.Container Container { get; }

        /// <summary>
        /// The transactional batch that can be used to store items.
        /// </summary>
        /// <remarks>The transactional batch exposed does delay the actual batch operations up to the point when the storage
        /// session is actually committed to avoid running into transaction timeouts unnecessarily. Furthermore all stream
        /// resources will be properly disposed by the infrastructure after the batch has been completed.</remarks>
        TransactionalBatch Batch { get; }
    }
}