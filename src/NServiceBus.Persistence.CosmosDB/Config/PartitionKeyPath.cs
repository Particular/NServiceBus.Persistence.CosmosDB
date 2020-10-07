namespace NServiceBus
{
    /// <summary>
    ///
    /// </summary>
    public readonly struct PartitionKeyPath
    {
        readonly string partitionKeyPath;

        /// <summary>
        ///
        /// </summary>
        public PartitionKeyPath(string partitionKeyPath)
        {
            this.partitionKeyPath = partitionKeyPath;
        }

        /// <summary>
        ///
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