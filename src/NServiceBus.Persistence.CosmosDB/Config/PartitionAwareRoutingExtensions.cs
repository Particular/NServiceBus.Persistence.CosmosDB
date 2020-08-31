namespace NServiceBus.Persistence.CosmosDB
{
    using Configuration.AdvancedExtensibility;
    using Transport;

    /// <summary>
    ///
    /// </summary>
    public static class PartitionAwareRoutingExtensions
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="routingSettings"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static PartitionAwareConfiguration Partition<T>(this RoutingSettings<T> routingSettings) where T : TransportDefinition
        {
            var settings = routingSettings.GetSettings();

            if (settings.TryGet(out PartitionAwareConfiguration config))
            {
                return config;
            }

            config = new PartitionAwareConfiguration(routingSettings);
            settings.Set(config);

            return config;
        }
    }
}