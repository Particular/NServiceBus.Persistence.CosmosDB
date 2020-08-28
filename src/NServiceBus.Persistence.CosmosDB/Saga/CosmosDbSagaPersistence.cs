namespace NServiceBus.Features
{
    using System;
    using System.Threading.Tasks;
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
            var serializerSettings = context.Settings.Get<JsonSerializerSettings>(SettingsKeys.Sagas.JsonSerializerSettings);
            var sagaMetadataCollection = context.Settings.Get<SagaMetadataCollection>();

            var cosmosClient = clientFactory();

            context.RegisterStartupTask(new InitializeContainers(cosmosClient, databaseName, sagaMetadataCollection));

            context.Container.ConfigureComponent(builder => new SagaPersister(serializerSettings, cosmosClient, databaseName), DependencyLifecycle.SingleInstance);
        }

        class InitializeContainers : FeatureStartupTask
        {
            CosmosClient cosmosClient;
            SagaMetadataCollection sagaMetadataCollection;
            string databaseName;

            public InitializeContainers(CosmosClient cosmosClient, string databaseName, SagaMetadataCollection sagaMetadataCollection)
            {
                this.databaseName = databaseName;
                this.sagaMetadataCollection = sagaMetadataCollection;
                this.cosmosClient = cosmosClient;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return cosmosClient.PopulateContainers(databaseName, sagaMetadataCollection);
            }

            protected override Task OnStop(IMessageSession session)
            {
                // for now here
                cosmosClient.Dispose();
                return Task.CompletedTask;
            }
        }
    }
}