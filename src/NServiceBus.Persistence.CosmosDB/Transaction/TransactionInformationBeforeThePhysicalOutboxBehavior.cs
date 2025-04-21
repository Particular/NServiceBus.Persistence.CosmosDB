namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Pipeline;

class TransactionInformationBeforeThePhysicalOutboxBehavior(IPartitionKeyFromHeadersExtractor partitionKeyExtractor, IContainerInformationFromHeadersExtractor containerInformationExtractor)
    : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
{
    public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
    {
        if (partitionKeyExtractor.TryExtract(context.Message.Headers, out PartitionKey? partitionKey))
        {
            // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
            if (partitionKey.HasValue)
            {
                context.Extensions.Set(partitionKey.Value);
            }
        }

        if (containerInformationExtractor.TryExtract(context.Message.Headers, out ContainerInformation? containerInformation))
        {
            // once we move to nullable reference type we can annotate the partition key with NotNullWhenAttribute and get rid of this check
            if (containerInformation.HasValue)
            {
                context.Extensions.Set(containerInformation.Value);
            }
        }

        return next(context);
    }

    public class RegisterStep(PartitionKeyExtractor partitionKeyExtractor, ContainerInformationExtractor containerInformationExtractor)
        : Pipeline.RegisterStep(nameof(TransactionInformationBeforeThePhysicalOutboxBehavior),
            typeof(TransactionInformationBeforeThePhysicalOutboxBehavior),
            "Populates the transaction information before the physical outbox.",
            b =>
            {
                IEnumerable<IPartitionKeyFromHeadersExtractor> partitionKeyFromHeadersExtractors = b.GetServices<IPartitionKeyFromHeadersExtractor>();
                foreach (IPartitionKeyFromHeadersExtractor extractor in partitionKeyFromHeadersExtractors)
                {
                    partitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor);
                }

                IEnumerable<IContainerInformationFromHeadersExtractor> containerInformationFromHeadersExtractors = b.GetServices<IContainerInformationFromHeadersExtractor>();
                foreach (IContainerInformationFromHeadersExtractor extractor in containerInformationFromHeadersExtractors)
                {
                    containerInformationExtractor.ExtractContainerInformationFromHeaders(extractor);
                }

                return new TransactionInformationBeforeThePhysicalOutboxBehavior(partitionKeyExtractor, containerInformationExtractor);
            });
}