namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

// The overloads with the extractor argument state are there to enable low allocation scenarios (avoiding closure allocations)
partial class PartitionKeyExtractor : IPartitionKeyFromHeadersExtractor, IPartitionKeyFromMessageExtractor
{
    readonly HashSet<Type> extractPartitionKeyFromMessagesTypes = [];

    readonly List<IPartitionKeyFromMessageExtractor> extractPartitionKeyFromMessages =
        [];

    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        // deliberate use of a for loop
        for (int index = 0; index < extractPartitionKeyFromMessages.Count; index++)
        {
            IPartitionKeyFromMessageExtractor extractor = extractPartitionKeyFromMessages[index];
            if (extractor.TryExtract(message, headers, out partitionKey))
            {
                return true;
            }
        }

        partitionKey = null;
        return false;
    }

    public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractPartitionKeyFromMessage<TMessage, Func<TMessage, PartitionKey>>((msg, _, invoker) => invoker(msg), extractor);

    public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractPartitionKeyFromMessage<TMessage, (Func<TMessage, TArg, PartitionKey>, TArg)>((msg, _, args) =>
        {
            (Func<TMessage, TArg, PartitionKey> invoker, TArg arg) = args;
            return invoker(msg, arg);
        }, (extractor, extractorArgument));

    public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, IReadOnlyDictionary<string, string>, PartitionKey> extractor) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractPartitionKeyFromMessage<TMessage, Func<TMessage, IReadOnlyDictionary<string, string>, PartitionKey>>((msg, headers, invoker) => invoker(msg, headers), extractor);

    public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor,
        TArg extractorArgument)
    {
        if (extractPartitionKeyFromMessagesTypes.Add(typeof(TMessage)))
        {
            ExtractPartitionKeyFromMessages(new PartitionKeyFromMessageExtractor<TMessage, TArg>(extractor, extractorArgument));
        }
        else
        {
            throw new ArgumentException($"The message type '{typeof(TMessage).FullName}' is already being handled by a message extractor and cannot be processed by another one.", nameof(TMessage));
        }
    }

    public void ExtractPartitionKeyFromMessages(IPartitionKeyFromMessageExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);

        extractPartitionKeyFromMessages.Add(extractor);
    }

    sealed class PartitionKeyFromMessageExtractor<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor, TArg argument = default)
        : IPartitionKeyFromMessageExtractor
    {
        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            if (message is TMessage typedMessage)
            {
                partitionKey = extractor(typedMessage, headers, argument);
                return true;
            }

            partitionKey = null;
            return false;
        }
    }
}