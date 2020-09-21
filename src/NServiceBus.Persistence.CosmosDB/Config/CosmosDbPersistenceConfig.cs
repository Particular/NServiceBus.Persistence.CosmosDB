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
        /// Sets the default container name and the partition key path that will be used.
        /// </summary>
        /// <remarks>When the default container is not set the container information needs to be provided as part of the message handling pipeline.</remarks>
        public static PersistenceExtensions<CosmosDbPersistence> DefaultContainer(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string containerName, string partitionKeyPath)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            persistenceExtensions.GetSettings().Set(new ContainerInformation(containerName, new PartitionKeyPath(partitionKeyPath)));

            return persistenceExtensions;
        }

        /// <summary>
        /// Disables the container creation.
        /// </summary>
        public static void DisableContainerCreation(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            var installerSettings = persistenceExtensions.GetSettings().GetOrCreate<InstallerSettings>();
            installerSettings.Disabled = true;
        }

        /// <summary>
        /// Enable support for sagas migrated from Azure Storage Persistence.
        /// </summary>
        public static void EnableMigrationMode(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            persistenceExtensions.GetSettings().Set(SettingsKeys.EnableMigrationMode, true);
        }
    }
}