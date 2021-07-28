namespace NServiceBus.Persistence.CosmosDB
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
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

            var migrationModeEnabled = context.Settings.GetOrDefault<bool>(SettingsKeys.EnableMigrationMode);

            context.Services.AddTransient(builder => new SagaPersister(serializer, migrationModeEnabled));
        }
    }
}