﻿namespace NServiceBus
{
    using System;
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

            var settings = persistenceExtensions.GetSettings();

            if (settings.HasExplicitValue(SettingsKeys.CosmosClient) || settings.HasExplicitValue(SettingsKeys.ResolveClientFromContainer))
            {
                throw new InvalidOperationException(clientRegistrationExceptionMessage);
            }

            settings.Set(SettingsKeys.CosmosClient, new ClientHolder {Client = cosmosClient});

            return persistenceExtensions;
        }

        /// <summary>
        /// Connection string to use for sagas storage.
        /// </summary>
        /// TODO: Discuss if we can drop this in favor of just providing CosmosClient above.
        public static PersistenceExtensions<CosmosDbPersistence> ConnectionString(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            var settings = persistenceExtensions.GetSettings();

            if (settings.HasExplicitValue(SettingsKeys.CosmosClient) || settings.HasExplicitValue(SettingsKeys.ResolveClientFromContainer))
            {
                throw new InvalidOperationException(clientRegistrationExceptionMessage);
            }

            settings.Set(SettingsKeys.CosmosClient, new ClientHolder {Client = new CosmosClient(connectionString)});

            return persistenceExtensions;
        }

        /// <summary>
        /// Resolve CosmosClient from dependency injection container.
        /// </summary>
        /// <param name="persistenceExtensions"></param>
        public static PersistenceExtensions<CosmosDbPersistence> ResolveClientFromContainer(this PersistenceExtensions<CosmosDbPersistence> persistenceExtensions)
        {
            var settings = persistenceExtensions.GetSettings();

            if (settings.HasExplicitValue(SettingsKeys.CosmosClient))
            {
                throw new InvalidOperationException(clientRegistrationExceptionMessage);
            }

            settings.Set(SettingsKeys.ResolveClientFromContainer, true);

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

        static readonly string clientRegistrationExceptionMessage = $"Cosmos DB client has been already configured using .{nameof(CosmosClient)}(), .{nameof(ConnectionString)}() or .{nameof(ResolveClientFromContainer)}().";
    }
}