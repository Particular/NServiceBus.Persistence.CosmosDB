namespace NServiceBus.Persistence.CosmosDB;

using Features;
using Microsoft.Extensions.DependencyInjection;

class InstallerFeature : Feature
{
    public InstallerFeature() => DependsOn<SynchronizedStorage>();

    protected override void Setup(FeatureConfigurationContext context)
    {
        if (context.Settings.GetOrDefault<bool>(SettingsKeys.DisableInstaller))
        {
            return;
        }

        var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

        if (!context.Settings.TryGet(out ContainerInformation containerInformation))
        {
            return;
        }

        context.Services.AddSingleton(new InstallerSettings
        {
            DatabaseName = databaseName,
            ContainerInformation = containerInformation
        });

        // Uncomment once core adds this
        //context.RegisterInstaller<Installer>();
    }
}