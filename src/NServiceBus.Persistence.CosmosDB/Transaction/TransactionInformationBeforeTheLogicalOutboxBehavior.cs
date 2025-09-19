namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Pipeline;

class TransactionInformationBeforeTheLogicalOutboxBehavior(IPartitionKeyFromMessageExtractor partitionKeyExtractor, IContainerInformationFromMessagesExtractor containerInformationExtractor)
    : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
{
    public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
    {
        if (partitionKeyExtractor.TryExtract(context.Message.Instance, context.MessageHeaders, out PartitionKey? partitionKey))
        {
            // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
            // Null check to cover the scenario where a custom message extractor is configured, but the message field is null.
            if (partitionKey.HasValue && partitionKey.Value != PartitionKey.Null)
            {
                context.Extensions.Set(partitionKey.Value);
            }
        }

        if (containerInformationExtractor.TryExtract(context.Message.Instance, context.MessageHeaders, out ContainerInformation? containerInformation))
        {
            // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
            if (containerInformation.HasValue)
            {
                context.Extensions.Set(containerInformation.Value);
            }
        }

        return next(context);
    }

    public class RegisterStep : Pipeline.RegisterStep
    {
        public RegisterStep(PartitionKeyExtractor partitionKeyExtractor,
            ContainerInformationExtractor containerInformationExtractor) :
            base(nameof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                typeof(TransactionInformationBeforeTheLogicalOutboxBehavior),
                "Populates the transaction information before the logical outbox.",
                b =>
                {
                    IEnumerable<IPartitionKeyFromMessageExtractor> partitionKeyExtractors = b.GetServices<IPartitionKeyFromMessageExtractor>();
                    foreach (IPartitionKeyFromMessageExtractor extractor in partitionKeyExtractors)
                    {
                        partitionKeyExtractor.ExtractPartitionKeyFromMessages(extractor);
                    }

                    IEnumerable<IContainerInformationFromMessagesExtractor> containerInformationFromMessagesExtractors = b.GetServices<IContainerInformationFromMessagesExtractor>();
                    foreach (IContainerInformationFromMessagesExtractor extractor in containerInformationFromMessagesExtractors)
                    {
                        containerInformationExtractor.ExtractContainerInformationFromMessage(extractor);
                    }

                    return new TransactionInformationBeforeTheLogicalOutboxBehavior(partitionKeyExtractor, containerInformationExtractor);
                }) =>
            InsertBeforeIfExists(nameof(LogicalOutboxBehavior));
    }
}