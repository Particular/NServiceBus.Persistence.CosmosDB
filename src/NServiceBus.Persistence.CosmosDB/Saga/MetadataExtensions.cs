namespace NServiceBus.Persistence.CosmosDB
{
    static partial class MetadataExtensions
    {
        internal const string SagaDataContainerSchemaVersionMetadataKey = "SagaDataContainer" + MetadataKeySchemaVersionSuffix;
        internal const string SagaDataContainerFullTypeNameMetadataKey = "SagaDataContainer-FullTypeName";
    }
}