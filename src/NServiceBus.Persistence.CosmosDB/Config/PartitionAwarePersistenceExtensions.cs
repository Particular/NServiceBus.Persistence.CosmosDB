namespace NServiceBus.Persistence.CosmosDB
{
    using Configuration.AdvancedExtensibility;

    /// <summary>
    ///
    /// </summary>
    public static class PartitionAwarePersistenceExtensions
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="persistenceSettings"></param>
        /// <returns></returns>
        public static PartitionAwareConfiguration Partition(this PersistenceExtensions<CosmosDbPersistence> persistenceSettings)
        {
            var settings = persistenceSettings.GetSettings();

            if (settings.TryGet(out PartitionAwareConfiguration config))
            {
                return config;
            }

            config = new PartitionAwareConfiguration(persistenceSettings);
            settings.Set(config);

            return config;
        }
    }
}