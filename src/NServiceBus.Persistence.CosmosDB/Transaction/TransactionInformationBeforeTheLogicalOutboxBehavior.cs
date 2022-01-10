namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    class TransactionInformationBeforeTheLogicalOutboxBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        IEnumerable<IExtractTransactionInformationFromMessages> extractTransactionInformationFromMessages;

        public TransactionInformationBeforeTheLogicalOutboxBehavior(IEnumerable<IExtractTransactionInformationFromMessages> extractTransactionInformationFromMessages) =>
            this.extractTransactionInformationFromMessages = extractTransactionInformationFromMessages;

        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            foreach (var extractTransactionInformationFromMessage in extractTransactionInformationFromMessages)
            {
                if (extractTransactionInformationFromMessage.TryExtract(context.Message.Instance, out var partitionKey,
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
            public RegisterStep() : base(nameof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                typeof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                "Populates the transaction information before the logical outbox.",
                b => new TransactionInformationBeforeTheLogicalOutboxBehavior(b.GetServices<IExtractTransactionInformationFromMessages>())) =>
                InsertBeforeIfExists(nameof(LogicalOutboxBehavior));
        }
    }
}