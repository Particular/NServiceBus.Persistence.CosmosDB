﻿namespace NServiceBus
{
    using Features;
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
            Defaults(s =>
            {
                s.SetDefault(new InstallerSettings());
                s.SetDefault(SettingsKeys.DatabaseName, "NServiceBus");
                s.SetDefault(SettingsKeys.ContainerName, s.EndpointName());
                s.SetDefault(new PartitionKeyPath("/Id"));
            });

            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<CosmosDbSagaPersistence>());
            Supports<StorageType.Outbox>(s => s.EnableFeatureByDefault<OutboxStorage>());
        }
    }
}