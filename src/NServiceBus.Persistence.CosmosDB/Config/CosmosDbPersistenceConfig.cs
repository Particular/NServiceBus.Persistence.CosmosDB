namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    /// <summary>
    /// Configuration extensions for CosmosDB Core API Persistence
    /// </summary>
    public static partial class CosmosDbPersistenceConfig
    {
        /// <summary>
        /// Override the default MongoClient creation by providing a pre-configured IMongoClient
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> MongoClient(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, CosmosClient cosmosClient)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(cosmosClient), cosmosClient);

            persistenceExtensions.GetSettings().Set(SettingsKeys.CosmosClient, (Func<CosmosClient>)(() => cosmosClient));
            return persistenceExtensions;
        }

        /// <summary>
        /// Connection string to use for sagas storage.
        /// </summary>
        public static PersistenceExtensions<CosmosDbPersistence> ConnectionString(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            persistenceExtensions.GetSettings().Set(SettingsKeys.ConnectionString, connectionString);

            return persistenceExtensions;
        }
    }
}