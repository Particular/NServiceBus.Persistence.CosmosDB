namespace NServiceBus.Persistence.CosmosDB
{
    class InstallerSettings
    {
        public bool Disabled { get; set; }
        public string ContainerName { get; set; }
        public string PartitionKeyPath { get; set; }
        public string DatabaseName { get; set; }
    }
}