namespace NServiceBus
{
    using System;
    using Features;
    using Microsoft.Azure.Cosmos;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// CosmosDB Core API persistence
    /// </summary>
    public class CosmosDbPersistence : PersistenceDefinition
    {
        internal CosmosDbPersistence()
        {
            Defaults(s =>
            {
                s.SetDefault(SettingsKeys.CosmosClient, (Func<CosmosClient>)(() =>
                {
                    if (!s.TryGet<string>(SettingsKeys.ConnectionString, out var connectionString))
                    {
                        throw new InvalidOperationException("Meaningful exception");
                    }
                    return new CosmosClient(connectionString);
                }));
            });

            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<CosmosDbSagaPersistence>());
        }
    }
}