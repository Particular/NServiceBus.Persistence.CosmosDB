namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Features;
    using Microsoft.Extensions.DependencyInjection;

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
            foreach (var extractTransactionInformationFromHeader in extractTransactionInformationFromHeaders)
            {
                context.Services.AddSingleton(extractTransactionInformationFromHeader);
            }

            var extractTransactionInformationFromMessages = context.Settings.Get<List<IExtractTransactionInformationFromMessages>>();
            foreach (var extractTransactionInformationFromMessage in extractTransactionInformationFromMessages)
            {
                context.Services.AddSingleton(extractTransactionInformationFromMessage);
            }

            context.Pipeline.Register<TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep>();
            context.Pipeline.Register(typeof(TransactionInformationBeforeThePhysicalOutboxBehavior), "Populates the transaction information before the physical outbox.");
        }
    }
}