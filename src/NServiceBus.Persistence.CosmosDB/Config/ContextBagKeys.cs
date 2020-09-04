namespace NServiceBus.Persistence.CosmosDB
{
    static class ContextBagKeys
    {
        const string baseName = "CosmosDB.";
        public const string PartitionKeyPath = baseName + nameof(PartitionKeyPath);
        public const string LogicalMessageId = baseName + nameof(LogicalMessageId);
    }
}