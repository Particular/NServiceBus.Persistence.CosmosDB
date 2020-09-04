namespace NServiceBus.Persistence.CosmosDB
{
    static partial class MetadataExtensions
    {
        internal const string OutboxDataContainerSchemaVersionMetadataKey = "OutboxDataContainer" + MetadataKeySchemaVersionSuffix;
    }
}