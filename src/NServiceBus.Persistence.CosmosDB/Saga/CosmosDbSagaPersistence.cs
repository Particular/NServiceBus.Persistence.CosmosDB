namespace NServiceBus.Features
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Sagas;
    using Persistence.CosmosDB;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            Defaults(s => s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator()));
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var clientFactory = context.Settings.Get<Func<CosmosClient>>(SettingsKeys.CosmosClient);
            var databaseName = context.Settings.Get<string>(SettingsKeys.Sagas.DatabaseName);
            var containerName = context.Settings.Get<string>(SettingsKeys.Sagas.ContainerName);
            var serializerSettings = context.Settings.Get<JsonSerializerSettings>(SettingsKeys.Sagas.JsonSerializerSettings);
            var sagaMetadataCollection = context.Settings.Get<SagaMetadataCollection>();

            var cosmosClient = clientFactory();

            context.Container.ConfigureComponent(builder => new SagaPersister(serializerSettings, cosmosClient, databaseName, containerName), DependencyLifecycle.SingleInstance);
        }
    }
}