﻿namespace NServiceBus
{
    using System.Collections.Generic;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    /// <summary>
    /// Configuration extensions for Cosmos DB Core API Persistence
    /// </summary>
    public static class CosmosPersistenceConfig
    {
        /// <summary>
        /// Override the default CosmosClient creation by providing a pre-configured CosmosClient
        /// </summary>
        /// <remarks>The lifetime of the provided client is assumed to be controlled by the caller of this method and thus the client will not be disposed.</remarks>
        public static PersistenceExtensions<CosmosPersistence> CosmosClient(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, CosmosClient cosmosClient)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(cosmosClient), cosmosClient);

            persistenceExtensions.GetSettings().Set<IProvideCosmosClient>(new CosmosClientProvidedByConfiguration { Client = cosmosClient });
            return persistenceExtensions;
        }

        /// <summary>
        /// Sets the database name
        /// </summary>
        public static PersistenceExtensions<CosmosPersistence> DatabaseName(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, string databaseName)
        {
            Guard.AgainstNullAndEmpty(nameof(databaseName), databaseName);

            persistenceExtensions.GetSettings().Set(SettingsKeys.DatabaseName, databaseName);

            return persistenceExtensions;
        }

        /// <summary>
        /// Sets the default container name and the partition key path that will be used.
        /// </summary>
        /// <remarks>When the default container is not set the container information needs to be provided as part of the message handling pipeline.</remarks>
        public static PersistenceExtensions<CosmosPersistence> DefaultContainer(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, string containerName, string partitionKeyPath)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            persistenceExtensions.GetSettings().Set(new ContainerInformation(containerName, new PartitionKeyPath(partitionKeyPath)));

            return persistenceExtensions;
        }

        /// <summary>
        /// Disables the container creation.
        /// </summary>
        public static void DisableContainerCreation(this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            var installerSettings = persistenceExtensions.GetSettings().GetOrCreate<InstallerSettings>();
            installerSettings.Disabled = true;
        }

        /// <summary>
        /// Enable support for sagas migrated from other persistence technologies by querying the saga from storage using a migrated saga id.
        /// </summary>
        [ObsoleteEx(Message = "Use persistence.Sagas().EnableMigrationMode() instead.", TreatAsErrorFromVersion = "2", RemoveInVersion = "3")]
        public static void EnableMigrationMode(this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);

            persistenceExtensions.GetSettings().GetOrCreate<SagaPersistenceConfiguration>().EnableMigrationMode();
        }

        /// <summary>
        /// Obtains the saga persistence configuration options.
        /// </summary>
        public static SagaPersistenceConfiguration Sagas(this PersistenceExtensions<CosmosPersistence> persistenceExtensions) =>
            persistenceExtensions.GetSettings().GetOrCreate<SagaPersistenceConfiguration>();
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="persistenceExtensions"></param>
        /// <param name="extractor"></param>
        /// <returns></returns>
        // TODO: Better name? Nesting on some type?
        public static PersistenceExtensions<CosmosPersistence> ExtractWith(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, TransactionInformationExtractor extractor)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(extractor), extractor);

            persistenceExtensions.ExtractFromHeaders(extractor);
            persistenceExtensions.ExtractFromMessages(extractor);
            return persistenceExtensions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="persistenceExtensions"></param>
        /// <param name="extractor"></param>
        /// <returns></returns>
        // TODO: Better name? Nesting on some type?
        public static PersistenceExtensions<CosmosPersistence> ExtractFromHeaders(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, ITransactionInformationFromHeadersExtractor extractor)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(extractor), extractor);

            var settings = persistenceExtensions.GetSettings();
            settings.GetOrCreate<List<ITransactionInformationFromHeadersExtractor>>().Add(extractor);
            return persistenceExtensions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="persistenceExtensions"></param>
        /// <param name="extractor"></param>
        /// <returns></returns>
        // TODO: Better name? Nesting on some type?
        public static PersistenceExtensions<CosmosPersistence> ExtractFromMessages(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, ITransactionInformationFromMessagesExtractor extractor)
        {
            Guard.AgainstNull(nameof(persistenceExtensions), persistenceExtensions);
            Guard.AgainstNull(nameof(extractor), extractor);

            var settings = persistenceExtensions.GetSettings();
            settings.GetOrCreate<List<ITransactionInformationFromMessagesExtractor>>().Add(extractor);
            return persistenceExtensions;
        }
    }
}