namespace NServiceBus.Persistence.CosmosDB;

using Microsoft.Azure.Cosmos;

class ContainerHolder(Container container, PartitionKeyPath partitionKeyPath)
{
    public Container Container { get; set; } = container;
    public PartitionKeyPath PartitionKeyPath { get; set; } = partitionKeyPath;
}