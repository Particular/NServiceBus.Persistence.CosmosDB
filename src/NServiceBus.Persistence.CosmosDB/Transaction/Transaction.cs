namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
            var fromHeaderTypesAdded = new HashSet<Type>();
            foreach (var extractTransactionInformationFromHeader in extractTransactionInformationFromHeaders)
            {
                context.Services.AddSingleton(extractTransactionInformationFromHeader);
                fromHeaderTypesAdded.Add(extractTransactionInformationFromHeader.GetType());
            }

            var extractTransactionInformationFromMessages = context.Settings.Get<List<IExtractTransactionInformationFromMessages>>();
            var fromMessageTypesAdded = new HashSet<Type>();
            foreach (var extractTransactionInformationFromMessage in extractTransactionInformationFromMessages)
            {
                context.Services.AddSingleton(extractTransactionInformationFromMessage);
                fromMessageTypesAdded.Add(extractTransactionInformationFromMessage.GetType());
            }

            // TODO: Add behind flag
            var fromHeadersExtractorTypes =
                from type in context.Settings.GetAvailableTypes()
                let interfaces = type.GetInterfaces()
                where interfaces.Any(t => t == typeof(IExtractTransactionInformationFromHeaders)) &&
                      type.Assembly != GetType().Assembly &&
                      !fromHeaderTypesAdded.Contains(type)
                select type;

            foreach (var headersExtractorType in fromHeadersExtractorTypes)
            {
                context.Services.AddSingleton(typeof(IExtractTransactionInformationFromHeaders), headersExtractorType);
            }

            var fromMessageExtractorTypes =
                from type in context.Settings.GetAvailableTypes()
                let interfaces = type.GetInterfaces()
                where interfaces.Any(t => t == typeof(IExtractTransactionInformationFromMessages)) &&
                      type.Assembly != GetType().Assembly &&
                      !fromMessageTypesAdded.Contains(type)
                select type;

            foreach (var messageExtractorType in fromMessageExtractorTypes)
            {
                context.Services.AddSingleton(typeof(IExtractTransactionInformationFromMessages), messageExtractorType);
            }

            context.Pipeline.Register<TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep>();
            context.Pipeline.Register(typeof(TransactionInformationBeforeThePhysicalOutboxBehavior), "Populates the transaction information before the physical outbox.");
        }
    }
}