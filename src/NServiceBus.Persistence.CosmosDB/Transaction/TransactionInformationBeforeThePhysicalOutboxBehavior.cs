namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class TransactionInformationBeforeThePhysicalOutboxBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly IPartitionKeyFromHeadersExtractor partitionKeyExtractor;
        readonly IContainerInformationFromHeadersExtractor containerInformationExtractor;

        public TransactionInformationBeforeThePhysicalOutboxBehavior(IPartitionKeyFromHeadersExtractor partitionKeyExtractor, IContainerInformationFromHeadersExtractor containerInformationExtractor)
        {
            this.partitionKeyExtractor = partitionKeyExtractor;
            this.containerInformationExtractor = containerInformationExtractor;
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (partitionKeyExtractor.TryExtract(context.Message.Headers, out var partitionKey))
            {
                // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
                if (partitionKey.HasValue)
                {
                    context.Extensions.Set(partitionKey.Value);
                }
            }
            if (containerInformationExtractor.TryExtract(context.Message.Headers, out var containerInformation))
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
            public RegisterStep(
                PartitionKeyExtractor partitionKeyExtractor, ContainerInformationExtractor containerInformationExtractor) :
                base(nameof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                typeof(TransactionInformationBeforeThePhysicalOutboxBehavior),
                "Populates the transaction information before the physical outbox.",
                b =>
                {
                    var partitionKeyFromHeadersExtractors = b.BuildAll<IPartitionKeyFromHeadersExtractor>();
                    foreach (var extractor in partitionKeyFromHeadersExtractors)
                    {
                        partitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor);
                    }

                    var containerInformationFromHeadersExtractors = b.BuildAll<IContainerInformationFromHeadersExtractor>();
                    foreach (var extractor in containerInformationFromHeadersExtractors)
                    {
                        containerInformationExtractor.ExtractContainerInformationFromHeaders(extractor);
                    }
                    return new TransactionInformationBeforeThePhysicalOutboxBehavior(partitionKeyExtractor, containerInformationExtractor);
                })
            {
            }
        }
    }
}