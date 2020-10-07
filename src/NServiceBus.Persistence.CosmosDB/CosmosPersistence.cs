namespace NServiceBus
{
    using Features;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// CosmosDB Core API persistence
    /// </summary>
    public class CosmosPersistence : PersistenceDefinition
    {
        internal CosmosPersistence()
        {
            Defaults(s =>
            {
                s.SetDefault(SettingsKeys.DatabaseName, "NServiceBus");
                s.SetDefault<IProvideCosmosClient>(new ThrowIfNoCosmosClientIsProvided());
                s.EnableFeatureByDefault<InstallerFeature>();
            });

            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<CosmosDbSagaPersistence>());
            Supports<StorageType.Outbox>(s => s.EnableFeatureByDefault<OutboxStorage>());
        }
    }
}