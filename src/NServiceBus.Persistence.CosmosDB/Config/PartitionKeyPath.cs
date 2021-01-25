namespace NServiceBus
{
    /// <summary>
    /// Represents a partition key path within a container.
    /// </summary>
    public struct PartitionKeyPath
    {
        readonly string partitionKeyPath;

        /// <summary>
        /// Initializes a new partition key path.
        /// </summary>
        /// <param name="partitionKeyPath">The partition key path.</param>\
        /// <remarks>No validation is done to assert whether the provided path matches the Cosmos DB conventions.</remarks>
        public PartitionKeyPath(string partitionKeyPath)
        {
            this.partitionKeyPath = partitionKeyPath;
        }

        /// <summary>
        /// Implicitly converts the partition key path into a string.
        /// </summary>
        public static implicit operator string(PartitionKeyPath path)
        {
            return path.partitionKeyPath;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return partitionKeyPath;
        }
    }
}