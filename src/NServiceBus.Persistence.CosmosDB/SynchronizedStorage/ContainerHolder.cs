namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    class ContainerHolder
    {
        public ContainerHolder(Container container, PartitionKeyPath partitionKeyPath)
        {
            Container = container;
            PartitionKeyPath = partitionKeyPath;
        }

        public Container Container { get; set; }
        public PartitionKeyPath PartitionKeyPath { get; set; }
    }
}
