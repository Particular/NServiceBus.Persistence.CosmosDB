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
                s.SetDefault(new List<ITransactionInformationFromHeadersExtractor>());
                s.SetDefault(new List<ITransactionInformationFromMessagesExtractor>());
            });
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var extractTransactionInformationFromHeaders = context.Settings.Get<List<ITransactionInformationFromHeadersExtractor>>();
            var extractTransactionInformationFromMessages = context.Settings.Get<List<ITransactionInformationFromMessagesExtractor>>();

            context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(extractTransactionInformationFromMessages));
            context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(extractTransactionInformationFromHeaders));
        }
    }
}