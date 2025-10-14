namespace NServiceBus.Persistence.CosmosDB;

class InstallerSettings
{
    public string DatabaseName { get; set; }
    public ContainerInformation ContainerInformation { get; set; }
}