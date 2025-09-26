namespace NServiceBus.Persistence.CosmosDB;

using Features;

sealed class Transaction : Feature
{
    public Transaction() => Defaults(s => s.SetDefault(new TransactionInformationConfiguration()));

    protected override void Setup(FeatureConfigurationContext context)
    {
        var transactionConfiguration = context.Settings.Get<TransactionInformationConfiguration>();

        context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(transactionConfiguration.PartitionKeyExtractor, transactionConfiguration.ContainerInformationExtractor));
        context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(transactionConfiguration.PartitionKeyExtractor, transactionConfiguration.ContainerInformationExtractor));
    }
}