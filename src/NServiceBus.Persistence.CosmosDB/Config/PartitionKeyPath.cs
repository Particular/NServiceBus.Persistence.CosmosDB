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
        /// <param name="partitionKeyPath"></param>
        public PartitionKeyPath(string partitionKeyPath)
        {
            this.partitionKeyPath = partitionKeyPath;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static implicit operator string(PartitionKeyPath path)
        {
            return path.partitionKeyPath;
        }
    }
}