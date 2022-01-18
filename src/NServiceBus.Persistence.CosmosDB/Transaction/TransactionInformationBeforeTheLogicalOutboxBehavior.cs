namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class TransactionInformationBeforeTheLogicalOutboxBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        readonly IPartitionKeyFromMessageExtractor partitionKeyExtractor;
        readonly IContainerInformationFromMessagesExtractor containerInformationExtractor;

        public TransactionInformationBeforeTheLogicalOutboxBehavior(IPartitionKeyFromMessageExtractor partitionKeyExtractor, IContainerInformationFromMessagesExtractor containerInformationExtractor)
        {
            this.partitionKeyExtractor = partitionKeyExtractor;
            this.containerInformationExtractor = containerInformationExtractor;
        }

        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            if (partitionKeyExtractor.TryExtract(context.Message.Instance, context.MessageHeaders, out var partitionKey))
            {
                // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
                if (partitionKey.HasValue)
                {
                    context.Extensions.Set(partitionKey.Value);
                }
            }
            if (containerInformationExtractor.TryExtract(context.Message.Instance, context.MessageHeaders, out var containerInformation))
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
                    var partitionKeyExtractors = b.BuildAll<IPartitionKeyFromMessageExtractor>();
                    foreach (var extractor in partitionKeyExtractors)
                    {
                        partitionKeyExtractor.ExtractPartitionKeyFromMessages(extractor);
                    }

                    var containerInformationFromMessagesExtractors = b.BuildAll<IContainerInformationFromMessagesExtractor>();
                    foreach (var extractor in containerInformationFromMessagesExtractors)
                    {
                        containerInformationExtractor.ExtractContainerInformationFromMessage(extractor);
                    }
                    return new TransactionInformationBeforeTheLogicalOutboxBehavior(partitionKeyExtractor, containerInformationExtractor);
                }) =>
                InsertBeforeIfExists(nameof(LogicalOutboxBehavior));
        }
    }
}