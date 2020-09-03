namespace NServiceBus.Persistence.CosmosDB
{
    static class SettingsKeys
    {
        const string baseName = "CosmosDB.";
        public const string CosmosClient = baseName + nameof(CosmosClient);
        public const string ConnectionString =  nameof(baseName) + nameof(ConnectionString);
        public const string DatabaseName = nameof(baseName) + nameof(DatabaseName);

        internal static class Sagas
        {
            public const string JsonSerializerSettings =  nameof(baseName) + nameof(Sagas) + "." + nameof(JsonSerializerSettings);
        }
    }
}