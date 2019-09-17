namespace NServiceBus.Features
{
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;

    class CosmosDbSubscriptionsPersistence : Feature
    {
        public CosmosDbSubscriptionsPersistence()
        {
            DependsOn<MessageDrivenSubscriptions>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            // should have an API to share the connection string and the database name
            var connectionString = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasConnectionString);
            var databaseName = context.Settings.Get<string>(WellKnownConfigurationKeys.SagasDatabaseName);
            var containerName = context.Settings.Get<string>(WellKnownConfigurationKeys.SubscriptionsContainerName);

            // TODO: should we allow customers to override the default CosmosClientOptions?
            //MaxRetryAttemptsOnRateLimitedRequests = 9,
            //MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            var cosmosClient = new CosmosClient(connectionString);

            // TODO: CosmosClient is IDisposable, will it be disposed properly from the container?
            context.Container.RegisterSingleton(new SubscriptionPersister(cosmosClient, databaseName, containerName));

        }
    }
}