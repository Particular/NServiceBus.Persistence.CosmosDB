namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    class TransactionInformationBeforeThePhysicalOutboxBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly ITransactionInformationFromHeadersExtractor extractor;

        public TransactionInformationBeforeThePhysicalOutboxBehavior(ITransactionInformationFromHeadersExtractor extractor) =>
            this.extractor = extractor;

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (extractor.TryExtract(context.Message.Headers, out var partitionKey,
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
            public RegisterStep(
                TransactionInformationExtractor transactionInformationExtractor) :
                base(nameof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                typeof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                "Populates the transaction information before the physical outbox.",
                b =>
                {
                    var extractors = b.GetServices<ITransactionInformationFromHeadersExtractor>();
                    foreach (var extractor in extractors)
                    {
                        transactionInformationExtractor.ExtractFromHeaders(extractor);
                    }
                    return new TransactionInformationBeforeThePhysicalOutboxBehavior(transactionInformationExtractor);
                })
            {
            }
        }
    }
}