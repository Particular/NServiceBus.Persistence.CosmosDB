namespace NServiceBus.Persistence.CosmosDB
{
    class ContainerHolder
    {
        public ContainerHolder(Microsoft.Azure.Cosmos.Container container, PartitionKeyPath partitionKeyPath)
        {
            Container = container;
            PartitionKeyPath = partitionKeyPath;
        }

        public Microsoft.Azure.Cosmos.Container Container { get; set; }
        public string PartitionKeyPath { get; set; }
    }
}
