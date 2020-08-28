namespace NServiceBus.Features
{
    using Microsoft.Azure.Cosmos;
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
            var connectionString = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasConnectionString);
            var databaseName = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasDatabaseName);
            var containerName = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasContainerName);

            // TODO: should we allow customers to override the default CosmosClientOptions?
            //MaxRetryAttemptsOnRateLimitedRequests = 9,
            //MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            var cosmosClient = new CosmosClient(connectionString);

            // TODO: CosmosClient is IDisposable, will it be disposed properly from the container?
            context.Container.ConfigureComponent(builder => new SagaPersister(cosmosClient, databaseName, containerName), DependencyLifecycle.SingleInstance);
        }
    }
}