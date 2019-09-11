namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Persistence.CosmosDB;

    public static partial class CosmosDbPersistenceConfig
    {
        /// <summary>
        /// Exposes saga specific settings.
        /// </summary>
        public static SagaSettings SagaSettings(this PersistenceExtensions<CosmosDbPersistence> configuration)
        {
            return new SagaSettings(configuration.GetSettings());
        }
    }
}