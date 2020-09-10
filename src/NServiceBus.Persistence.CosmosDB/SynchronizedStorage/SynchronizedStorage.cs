﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<ClientHolder>(SettingsKeys.CosmosClient).Client;

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string");
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);
            var containerName = context.Settings.Get<string>(SettingsKeys.ContainerName);

            if (!context.Settings.TryGet<PartitionKeyPath>(out var partitionKeyPath))
            {
                partitionKeyPath = new PartitionKeyPath(string.Empty); //TODO: What is the right default? "id"?
            }

            var container = client.GetContainer(databaseName, containerName);

            var containerHolder = new ContainerHolder(container, partitionKeyPath);

            context.Container.ConfigureComponent(() => containerHolder, DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionFactory>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}