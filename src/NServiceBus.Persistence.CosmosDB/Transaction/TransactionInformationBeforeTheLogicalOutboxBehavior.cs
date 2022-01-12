namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    class TransactionInformationBeforeTheLogicalOutboxBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        ITransactionInformationFromMessagesExtractor extractor;

        public TransactionInformationBeforeTheLogicalOutboxBehavior(ITransactionInformationFromMessagesExtractor extractor) =>
            this.extractor = extractor;

        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            if (extractor.TryExtract(context.Message.Instance, out var partitionKey,
                    out var containerInformation))
            {
                // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
                if (partitionKey.HasValue)
                {
                    context.Extensions.Set(partitionKey.Value);
                }

                if (containerInformation.HasValue)
                {
                    context.Extensions.Set(containerInformation.Value);
                }
            }
            return next(context);
        }

        public class RegisterStep : Pipeline.RegisterStep
        {
            public RegisterStep(TransactionInformationExtractor transactionInformationExtractor) :
                base(nameof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                typeof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                "Populates the transaction information before the logical outbox.",
                b =>
                {
                    var extractors = b.GetServices<ITransactionInformationFromMessagesExtractor>();
                    foreach (var extractor in extractors)
                    {
                        transactionInformationExtractor.ExtractFromMessages(extractor);
                    }
                    return new TransactionInformationBeforeTheLogicalOutboxBehavior(transactionInformationExtractor);
                }) =>
                InsertBeforeIfExists(nameof(LogicalOutboxBehavior));
        }
    }
}