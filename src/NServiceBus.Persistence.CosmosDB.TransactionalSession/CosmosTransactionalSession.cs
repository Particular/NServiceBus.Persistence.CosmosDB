namespace NServiceBus.TransactionalSession
{
    using Features;
    using Persistence.CosmosDB;
    using SynchronizedStorage = Persistence.CosmosDB.SynchronizedStorage;

    sealed class CosmosTransactionalSession : Feature
    {
        public CosmosTransactionalSession()
        {
            Defaults(s =>
            {
                s.GetOrCreate<TransactionInformationConfiguration>().ExtractPartitionKeyFromHeaders(new ControlMessagePartitionKeyExtractor());
                s.GetOrCreate<TransactionInformationConfiguration>().ExtractContainerInformationFromHeaders(new ControlMessageContainerInformationExtractor());
            });

            DependsOn<SynchronizedStorage>();
            DependsOn<TransactionalSession>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }
    }
}