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
        /// <remarks>The lifetime of the provided client is assumed to be controlled by the caller of this method and thus the client will not be disposed.</remarks>
        public static PersistenceExtensions<CosmosDbPersistence> CosmosClient(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, CosmosClient cosmosClient)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(cosmosClient), cosmosClient);

            persistenceExtensions.GetSettings().Set<IProvideCosmosClient>(new CosmosClientProvidedByConfiguration { Client = cosmosClient });
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

        /// <summary>
        /// Disables the container creation.
        /// </summary>
        public static void DisableContainerCreation(this PersistenceExtensions<CosmosDbPersistence> configuration)
        {
            Guard.AgainstNull(nameof(configuration), configuration);

            var installerSettings = configuration.GetSettings().GetOrCreate<InstallerSettings>();
            installerSettings.Disabled = true;
        }
    }
}