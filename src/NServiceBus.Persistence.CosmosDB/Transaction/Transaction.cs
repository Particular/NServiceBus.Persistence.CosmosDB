namespace NServiceBus.Persistence.CosmosDB
{
    using Features;

    sealed class Transaction : Feature
    {
        public Transaction() => Defaults(s => s.SetDefault(new TransactionInformationConfiguration()));

        protected override void Setup(FeatureConfigurationContext context)
        {
            var configuration = context.Settings.Get<TransactionInformationConfiguration>();

            context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
            context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
        }
    }
}