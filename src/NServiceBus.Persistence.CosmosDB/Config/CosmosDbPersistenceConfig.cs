namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;
    using Settings;

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
        /// Sets the database name
        /// </summary>
        public static ContainerSettings Container(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string containerName) //TODO: I am sure this is right, but I wanted to capture the requirement
        {
            Guard.AgainstNullAndEmpty(nameof(containerName), containerName);

            var settings = persistenceExtensions.GetSettings();

            settings.Set(SettingsKeys.ContainerName, containerName);

            return new ContainerSettings(settings);
        }
    }

    /// <summary>
    /// Settings for the Cosmos DB container (collection)
    /// </summary>
    public class ContainerSettings
    {
        /// <summary>
        /// </summary>
        public ContainerSettings(SettingsHolder settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// </summary>
        /// <param name="partitionKeyPath"></param>
        public void UsePartitionKeyPath(string partitionKeyPath)
        {
            Guard.AgainstNullAndEmpty(nameof(partitionKeyPath), partitionKeyPath);

            settings.Set(new PartitionKeyPath(partitionKeyPath));
        }

        readonly SettingsHolder settings;
    }
}