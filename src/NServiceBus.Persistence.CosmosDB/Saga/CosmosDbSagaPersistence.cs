namespace NServiceBus.Features
{
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Sagas;
    using Persistence.CosmosDB;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            DependsOn<Sagas>();
            Defaults(s => s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator()));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            // TODO: we need a CosmosClient cached once and forever. Should it be injected rather than the connection string?
            
            var connectionString = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasConnectionString);

            // TODO: should we allow customers to override the default CosmosClientOptions?
            //MaxRetryAttemptsOnRateLimitedRequests = 9,
            //MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            var cosmosClient = new CosmosClient(connectionString);

            // TODO: CosmosClient is IDisposable, will it be disposed properly from the container?
            context.Container.RegisterSingleton<ISagaPersister>(new SagaPersister(cosmosClient));
        }
    }
}