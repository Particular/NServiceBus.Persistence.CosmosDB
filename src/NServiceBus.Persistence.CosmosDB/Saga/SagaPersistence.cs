namespace NServiceBus.Persistence.CosmosDB
{
    using Features;
    using Newtonsoft.Json;
    using Sagas;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            Defaults(s =>
            {
                s.EnableFeatureByDefault<SynchronizedStorage>();
                s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator());
            });
            DependsOn<SynchronizedStorage>();
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            NonNativePubSubCheck.ThrowIfMessageDrivenPubSubInUse(context);

            var serializer = new JsonSerializer { ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver() };

            var options = context.Settings.GetOrDefault<SagaPersistenceConfiguration>() ?? new SagaPersistenceConfiguration();

            context.Container.ConfigureComponent(builder => new SagaPersister(serializer, options), DependencyLifecycle.SingleInstance);
        }
    }
}