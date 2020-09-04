namespace NServiceBus.Persistence.CosmosDB
{
    static class ContextBagKeys
    {
        const string baseName = "CosmosDB.";
        public const string PartitionKeyValue = baseName + nameof(PartitionKeyValue);
        public const string PartitionKeyPath = baseName + nameof(PartitionKeyPath);
        public const string LogicalMessageId = baseName + nameof(LogicalMessageId);
    }
}