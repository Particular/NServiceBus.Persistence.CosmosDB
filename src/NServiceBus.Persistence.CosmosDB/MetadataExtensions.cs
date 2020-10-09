namespace NServiceBus.Persistence.CosmosDB
{
    static partial class MetadataExtensions
    {
        internal const string MetadataKey = "_NServiceBus-Persistence-Metadata";
        internal const string MajorVersion = MetadataKey + "-Major-Version";
        internal const string MinorVersion = MetadataKey + "-Minor-Version";
    }
}