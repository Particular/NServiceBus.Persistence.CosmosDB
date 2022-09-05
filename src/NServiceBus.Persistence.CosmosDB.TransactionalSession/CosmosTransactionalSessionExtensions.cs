namespace NServiceBus.TransactionalSession;

using Configuration.AdvancedExtensibility;
using Features;

/// <summary>
/// Enables the transactional session feature.
/// </summary>
public static class CosmosTransactionalSessionExtensions
{
    /// <summary>
    /// Enables transactional session for this endpoint.
    /// </summary>
    /// <param name="persistenceExtensions"></param>
    /// <returns></returns>
    public static PersistenceExtensions<CosmosPersistence> EnableTransactionalSession(
        this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
    {
        persistenceExtensions.GetSettings().EnableFeatureByDefault(typeof(TransactionalSession));
        persistenceExtensions.GetSettings().EnableFeatureByDefault(typeof(CosmosTransactionalSession));

        return persistenceExtensions;
    }
}