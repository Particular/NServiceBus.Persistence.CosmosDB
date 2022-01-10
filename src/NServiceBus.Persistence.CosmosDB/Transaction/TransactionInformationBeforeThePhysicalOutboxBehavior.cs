namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Pipeline;

    class TransactionInformationBeforeThePhysicalOutboxBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly IEnumerable<IExtractTransactionInformationFromHeaders> extractTransactionInformationFromHeaders;

        public TransactionInformationBeforeThePhysicalOutboxBehavior(IEnumerable<IExtractTransactionInformationFromHeaders> extractTransactionInformationFromHeaders) =>
            this.extractTransactionInformationFromHeaders = extractTransactionInformationFromHeaders;

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            foreach (var extractTransactionInformationFromHeader in extractTransactionInformationFromHeaders)
            {
                if (extractTransactionInformationFromHeader.TryExtract(context.Message.Headers, out var partitionKey,
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
    }
}