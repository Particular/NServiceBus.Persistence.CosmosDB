namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Newtonsoft.Json;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<ClientHolder>(SettingsKeys.CosmosClient).Client;

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string");
            }


            context.Container.ConfigureComponent<StorageSessionFactory>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}