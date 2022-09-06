namespace NServiceBus.TransactionalSession
{
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
        public static PersistenceExtensions<CosmosPersistence> EnableTransactionalSession(
            this PersistenceExtensions<CosmosPersistence> persistenceExtensions)
        {
            persistenceExtensions.GetSettings().EnableFeatureByDefault<TransactionalSession>();
            persistenceExtensions.GetSettings().EnableFeatureByDefault<CosmosTransactionalSession>();
            return persistenceExtensions;
        }
    }
}