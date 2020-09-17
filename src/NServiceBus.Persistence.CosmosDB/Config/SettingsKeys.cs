namespace NServiceBus.Persistence.CosmosDB
{
    static class SettingsKeys
    {
        const string baseName = "CosmosDB.";
        public const string DatabaseName = nameof(baseName) + nameof(DatabaseName);
        public const string OutboxTimeToLiveInSeconds = nameof(baseName) + nameof(OutboxTimeToLiveInSeconds);
    }
}