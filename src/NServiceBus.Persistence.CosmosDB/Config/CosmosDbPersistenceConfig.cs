namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    /// <summary>
    /// Configuration extensions for CosmosDB Core API Persistence
    /// </summary>
    public static partial class CosmosDbPersistenceConfig
    {
        /// <summary>
        /// Override the default CosmosClient creation by providing a pre-configured CosmosClient
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> CosmosClient(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, CosmosClient cosmosClient)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(cosmosClient), cosmosClient);

            persistenceExtensions.GetSettings().Set(SettingsKeys.CosmosClient, cosmosClient);
            return persistenceExtensions;
        }

        /// <summary>
        /// Connection string to use for sagas storage.
        /// </summary>
        /// TODO: Discuss if we can drop this in favor of just providing CosmosClient above.
        public static PersistenceExtensions<CosmosDbPersistence> ConnectionString(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            persistenceExtensions.GetSettings().Set(SettingsKeys.CosmosClient, new CosmosClient(connectionString));

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
    }
}