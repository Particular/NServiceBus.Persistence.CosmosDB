namespace NServiceBus
{
    using Features;
    using Newtonsoft.Json;
    using Persistence;
    using Persistence.CosmosDB;
    using Persistence.CosmosDB.Outbox;

    /// <summary>
    /// CosmosDB Core API persistence
    /// </summary>
    public class CosmosDbPersistence : PersistenceDefinition
    {
        internal CosmosDbPersistence()
        {

            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<CosmosDbSagaPersistence>());
            Supports<StorageType.Outbox>(s => s.EnableFeatureByDefault<OutboxStorage>());
        }
    }
}