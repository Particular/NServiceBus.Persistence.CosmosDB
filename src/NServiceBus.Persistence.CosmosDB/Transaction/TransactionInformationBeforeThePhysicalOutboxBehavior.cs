namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    class TransactionInformationBeforeThePhysicalOutboxBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly IEnumerable<ITransactionInformationFromHeadersExtractor> transactionInformationFromHeaderExtractors;

        public TransactionInformationBeforeThePhysicalOutboxBehavior(IEnumerable<ITransactionInformationFromHeadersExtractor> transactionInformationFromHeaderExtractors) =>
            this.transactionInformationFromHeaderExtractors = transactionInformationFromHeaderExtractors;

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            foreach (var extractor in transactionInformationFromHeaderExtractors)
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
                    // first match wins
                    break;
                }
            }
            return next(context);
        }

        public class RegisterStep : Pipeline.RegisterStep
        {
            public RegisterStep(
                IEnumerable<ITransactionInformationFromHeadersExtractor> extractTransactionInformationFromHeaders) :
                base(nameof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                typeof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                "Populates the transaction information before the physical outbox.",
                b => new TransactionInformationBeforeThePhysicalOutboxBehavior(
                    extractTransactionInformationFromHeaders.Union(b.GetServices<ITransactionInformationFromHeadersExtractor>())))
            {
            }
        }
    }
}