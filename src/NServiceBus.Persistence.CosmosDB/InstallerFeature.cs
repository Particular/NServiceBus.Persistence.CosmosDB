namespace NServiceBus.Persistence.CosmosDB;

using Features;
using Microsoft.Extensions.DependencyInjection;

class InstallerFeature : Feature
{
    public InstallerFeature()
    {
        Defaults(s => s.SetDefault(new InstallerSettings()));
        DependsOn<SynchronizedStorage>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        InstallerSettings settings = context.Settings.Get<InstallerSettings>();
        context.Services.AddSingleton(settings);
        if (settings.Disabled)
        {
            return;
        }

        string databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

        if (!context.Settings.TryGet<ContainerInformation>(out ContainerInformation containerInformation))
        {
            settings.Disabled = true;
            return;
        }

        settings.ContainerName = containerInformation.ContainerName;
        settings.DatabaseName = databaseName;
        settings.PartitionKeyPath = containerInformation.PartitionKeyPath;
    }
}