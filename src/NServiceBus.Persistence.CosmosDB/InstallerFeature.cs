namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;

    class InstallerFeature : Feature
    {
        public InstallerFeature()
        {
            Defaults(s => s.SetDefault(new InstallerSettings()));
            DependsOn<SynchronizedStorage>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<InstallerSettings>();
            if (settings.Disabled)
            {
                return;
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);
            var containerName = context.Settings.Get<string>(SettingsKeys.ContainerName);
            var partitionKeyPath = context.Settings.Get<PartitionKeyPath>();

            var client = context.Settings.Get<ClientHolder>(SettingsKeys.CosmosClient).Client;

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string.");
            }

            settings.ContainerName = containerName;
            settings.DatabaseName = databaseName;
            settings.PartitionKeyPath = partitionKeyPath;
            settings.Client = client;
        }
    }
}