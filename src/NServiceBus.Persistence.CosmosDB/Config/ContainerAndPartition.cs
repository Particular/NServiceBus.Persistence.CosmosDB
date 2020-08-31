namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    /// <summary>
    ///
    /// </summary>
    public readonly struct ContainerAndPartition
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(ContainerAndPartition other)
        {
            return string.Equals(PartitionKey, other.PartitionKey, StringComparison.OrdinalIgnoreCase) && string.Equals(Container, other.Container, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is ContainerAndPartition other && Equals(other);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((PartitionKey != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(PartitionKey) : 0) * 397) ^ (Container != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Container) : 0);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(ContainerAndPartition left, ContainerAndPartition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(ContainerAndPartition left, ContainerAndPartition right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        ///
        /// </summary>
        public string PartitionKey { get; }
        /// <summary>
        ///
        /// </summary>
        public string Container { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="container"></param>
        /// <param name="partitionKey"></param>
        public ContainerAndPartition(string container, string partitionKey)
        {
            PartitionKey = partitionKey;
            Container = container;
        }

        /// <summary>
        ///
        /// </summary>
        public static ContainerAndPartition None { get; } = new ContainerAndPartition(null, null);
    }
}