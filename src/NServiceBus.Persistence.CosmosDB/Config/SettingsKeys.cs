namespace NServiceBus.Persistence.CosmosDB
{
    static class SettingsKeys
    {
        const string BaseName = "CosmosDB.";
        public const string DatabaseName = nameof(BaseName) + nameof(DatabaseName);
        public const string OutboxTimeToLiveInSeconds = nameof(BaseName) + nameof(OutboxTimeToLiveInSeconds);
    }
}