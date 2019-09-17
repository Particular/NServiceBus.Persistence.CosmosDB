namespace NServiceBus
{
    using Features;
    using Persistence;

    /// <summary>
    /// CosmosDB Core API persistence
    /// </summary>
    public class CosmosDbPersistence : PersistenceDefinition
    {
        internal CosmosDbPersistence()
        {
            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<CosmosDbSagaPersistence>());
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<CosmosDbSubscriptionsPersistence>());
        }
    }
}