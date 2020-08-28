namespace NServiceBus.Persistence.CosmosDB
{
    static class SettingsKeys
    {
        const string baseName = "CosmosDB.";
        public const string CosmosClient = baseName + nameof(CosmosClient);
        public const string ConnectionString =  nameof(baseName) + nameof(ConnectionString);

        internal static class Sagas
        {
            public const string DatabaseName =  nameof(baseName) + nameof(Sagas) + "." + nameof(DatabaseName);
            public const string ContainerName =  nameof(baseName) + nameof(Sagas) + "." + nameof(ContainerName);
            public const string JsonSerializerSettings =  nameof(baseName) + nameof(Sagas) + "." + nameof(JsonSerializerSettings);
        }
    }
}