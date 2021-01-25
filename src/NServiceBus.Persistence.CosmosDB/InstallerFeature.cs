namespace NServiceBus.Persistence.CosmosDB
{
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
            context.Container.ConfigureComponent(() => settings, DependencyLifecycle.SingleInstance);
            if (settings.Disabled)
            {
                return;
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

            if (!context.Settings.TryGet<ContainerInformation>(out var containerInformation))
            {
                settings.Disabled = true;
                return;
            }

            settings.ContainerName = containerInformation.ContainerName;
            settings.DatabaseName = databaseName;
            settings.PartitionKeyPath = containerInformation.PartitionKeyPath;
        }
    }
}