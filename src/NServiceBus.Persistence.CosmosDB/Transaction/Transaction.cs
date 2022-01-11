namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Features;

    class Transaction : Feature
    {
        public Transaction()
        {
            Defaults(s =>
            {
                s.SetDefault(new List<IExtractTransactionInformationFromHeaders>());
                s.SetDefault(new List<IExtractTransactionInformationFromMessages>());
            });
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var extractTransactionInformationFromHeaders = context.Settings.Get<List<IExtractTransactionInformationFromHeaders>>();
            var extractTransactionInformationFromMessages = context.Settings.Get<List<IExtractTransactionInformationFromMessages>>();

            context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(extractTransactionInformationFromMessages));
            context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(extractTransactionInformationFromHeaders));
        }
    }
}