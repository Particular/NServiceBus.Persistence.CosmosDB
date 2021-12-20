namespace NServiceBus.Persistence.CosmosDB
{
    using System.Linq;
    using Features;
    using Microsoft.Extensions.DependencyInjection;

    class Transaction : Feature
    {
        public Transaction()
        {
            Defaults(s =>
            {
                s.SetDefault<IExtractTransactionInformationFromHeaders>(new ExtractNothingFromHeaders());
                s.SetDefault<IExtractTransactionInformationFromMessages>(new ExtractNothingFromMessages());
            });
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            // TODO: Decide whether we want to allow multiple registrations
            if (!context.Services.Any(descriptor => descriptor.ServiceType == typeof(IExtractTransactionInformationFromHeaders)))
            {
                context.Services.AddSingleton(context.Settings.Get<IExtractTransactionInformationFromHeaders>());
            }

            // TODO: Decide whether we want to allow multiple registrations
            if (!context.Services.Any(descriptor => descriptor.ServiceType == typeof(IExtractTransactionInformationFromMessages)))
            {
                context.Services.AddSingleton(context.Settings.Get<IExtractTransactionInformationFromMessages>());
            }

            context.Pipeline.Register<TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep>();
            context.Pipeline.Register(typeof(TransactionInformationBeforeThePhysicalOutboxBehavior), "Populates the transaction information before the physical outbox.");
        }
    }
}