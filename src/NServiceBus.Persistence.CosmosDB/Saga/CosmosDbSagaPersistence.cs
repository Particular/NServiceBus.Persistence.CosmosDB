namespace NServiceBus.Features
{
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

            context.Container.RegisterSingleton<ISagaPersister>(new SagaPersister(connectionString));
        }
    }
}