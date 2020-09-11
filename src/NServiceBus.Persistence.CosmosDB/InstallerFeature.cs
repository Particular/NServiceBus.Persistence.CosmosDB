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
            if (settings.Disabled)
            {
                return;
            }
        }
    }
}