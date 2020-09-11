namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    class InstallerSettings
    {
        public bool Disabled { get; set; }
        public CosmosClient Client { get; set; }
        public string ContainerName { get; set; }
        public string PartitionKeyPath { get; set; }
        public string DatabaseName { get; set; }
    }
}