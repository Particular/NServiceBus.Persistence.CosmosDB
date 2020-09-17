namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    // Needed for the logical outbox to have the right partition key to complete an outbox transaction when SetAsDispatched() is invoked
    class SetAsDispatchedHolder
    {
        public PartitionKey PartitionKey  { get; set; }
        public ContainerHolder ContainerHolder { get; set; }
    }
}
