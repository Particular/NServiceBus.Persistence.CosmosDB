namespace NServiceBus.TransactionalSession;

using System;
using Configuration.AdvancedExtensibility;
using Features;
using Persistence.CosmosDB;

/// <summary>
/// Enables the transactional session feature.
/// </summary>
public static class CosmosTransactionalSessionExtensions
{
    /// <summary>
    /// Enables transactional session for this endpoint.
    /// </summary>
    public static PersistenceExtensions<CosmosPersistence> EnableTransactionalSession(
        this PersistenceExtensions<CosmosPersistence> persistenceExtensions) =>
        EnableTransactionalSession(persistenceExtensions, new TransactionalSessionOptions());

    /// <summary>
    /// Enables the transactional session for this endpoint using the specified TransactionalSessionOptions.
    /// </summary>
    public static PersistenceExtensions<CosmosPersistence> EnableTransactionalSession(this PersistenceExtensions<CosmosPersistence> persistenceExtensions,
        TransactionalSessionOptions transactionalSessionOptions)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);
        ArgumentNullException.ThrowIfNull(transactionalSessionOptions);

        var settings = persistenceExtensions.GetSettings();

        settings.Set(transactionalSessionOptions);

        if (!string.IsNullOrWhiteSpace(transactionalSessionOptions.ProcessorEndpoint))
        {
            settings.GetOrCreate<OutboxPersistenceConfiguration>().PartitionKey = transactionalSessionOptions.ProcessorEndpoint;
            settings.Set(OutboxStorage.ProcessorEndpointKey, transactionalSessionOptions.ProcessorEndpoint);
        }

        settings.EnableFeature<CosmosTransactionalSession>();

        return persistenceExtensions;
    }
}