namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    /// <summary>
    /// Configuration extensions for CosmosDB Core API Persistence
    /// </summary>
    public static class CosmosDbPersistenceConfig
    {
        /// <summary>
        /// Override the default CosmosClient creation by providing a pre-configured CosmosClient
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> CosmosClient(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, CosmosClient cosmosClient)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(cosmosClient), cosmosClient);

            persistenceExtensions.GetSettings().Set(SettingsKeys.CosmosClient, new ClientHolder {Client = cosmosClient});
            return persistenceExtensions;
        }

        /// <summary>
        /// Connection string to use for sagas storage.
        /// </summary>
        /// TODO: Discuss if we can drop this in favor of just providing CosmosClient above.
        public static PersistenceExtensions<CosmosDbPersistence> ConnectionString(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            persistenceExtensions.GetSettings().Set(SettingsKeys.CosmosClient, new ClientHolder {Client = new CosmosClient(connectionString)});

            return persistenceExtensions;
        }

        /// <summary>
        /// Sets the database name
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> DatabaseName(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string databaseName)
        {
            Guard.AgainstNullAndEmpty(nameof(databaseName), databaseName);

            persistenceExtensions.GetSettings().Set(SettingsKeys.DatabaseName, databaseName);

            return persistenceExtensions;
        }

        /// <summary>
        /// Sets container name and the partition key path
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> Container(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string containerName, string partitionKeyPath)
        {
            Guard.AgainstNullAndEmpty(nameof(containerName), containerName);

            var settings = persistenceExtensions.GetSettings();

            settings.Set(SettingsKeys.ContainerName, containerName);
            settings.Set(new PartitionKeyPath(partitionKeyPath));

            return persistenceExtensions;
        }
    }
}