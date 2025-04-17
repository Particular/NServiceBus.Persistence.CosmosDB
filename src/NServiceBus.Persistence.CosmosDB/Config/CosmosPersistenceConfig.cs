namespace NServiceBus;

using System;
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
        ArgumentNullException.ThrowIfNull(persistenceExtensions);
        ArgumentNullException.ThrowIfNull(cosmosClient);

        persistenceExtensions.GetSettings().Set<IProvideCosmosClient>(new CosmosClientProvidedByConfiguration { Client = cosmosClient });
        return persistenceExtensions;
    }

    /// <summary>
    /// Sets the database name
    /// </summary>
    public static PersistenceExtensions<CosmosPersistence> DatabaseName(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        persistenceExtensions.GetSettings().Set(SettingsKeys.DatabaseName, databaseName);

        return persistenceExtensions;
    }

    /// <summary>
    /// Sets the default container name and the partition key path that will be used.
    /// </summary>
    /// <remarks>When the default container is not set the container information needs to be provided as part of the message handling pipeline.</remarks>
    public static PersistenceExtensions<CosmosPersistence> DefaultContainer(this PersistenceExtensions<CosmosPersistence> persistenceExtensions, string containerName, string partitionKeyPath)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);

        persistenceExtensions.GetSettings().Set(new ContainerInformation(containerName, new PartitionKeyPath(partitionKeyPath)));

        return persistenceExtensions;
    }

    /// <summary>
    /// Disables the container creation.
    /// </summary>
    public static void DisableContainerCreation(this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);

        InstallerSettings installerSettings = persistenceExtensions.GetSettings().GetOrCreate<InstallerSettings>();
        installerSettings.Disabled = true;
    }

    /// <summary>
    /// Obtains the saga persistence configuration options.
    /// </summary>
    public static SagaPersistenceConfiguration Sagas(this PersistenceExtensions<CosmosPersistence> persistenceExtensions) =>
        persistenceExtensions.GetSettings().GetOrCreate<SagaPersistenceConfiguration>();

    /// <summary>
    /// Obtains the transaction information configuration options.
    /// </summary>
    public static TransactionInformationConfiguration TransactionInformation(this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);

        return persistenceExtensions.GetSettings().GetOrCreate<TransactionInformationConfiguration>();
    }
}