namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;

// The overloads with the extractor argument state are there to enable low allocation scenarios (avoiding closure allocations)
partial class ContainerInformationExtractor : IContainerInformationFromHeadersExtractor, IContainerInformationFromMessagesExtractor
{
    readonly HashSet<Type> extractContainerInformationFromMessagesTypes = [];

    readonly List<IContainerInformationFromMessagesExtractor> extractContainerInformationFromMessages =
        [];

    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        // deliberate use of a for loop
        for (int index = 0; index < extractContainerInformationFromMessages.Count; index++)
        {
            IContainerInformationFromMessagesExtractor extractor = extractContainerInformationFromMessages[index];
            if (extractor.TryExtract(message, headers, out containerInformation))
            {
                return true;
            }
        }

        containerInformation = null;
        return false;
    }

    public void ExtractContainerInformationFromMessage<TMessage>(ContainerInformation containerInformation) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromMessage<TMessage, ContainerInformation>((_, container) => container, containerInformation);

    public void ExtractContainerInformationFromMessage<TMessage>(Func<TMessage, ContainerInformation> extractor) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromMessage<TMessage, Func<TMessage, ContainerInformation>>((msg, _, invoker) => invoker(msg), extractor);

    public void ExtractContainerInformationFromMessage<TMessage, TArg>(Func<TMessage, TArg, ContainerInformation> extractor, TArg extractorArgument) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromMessage<TMessage, (TArg, Func<TMessage, TArg, ContainerInformation>)>((msg, _, args) =>
        {
            (TArg arg, Func<TMessage, TArg, ContainerInformation> invoker) = args;
            return invoker(msg, arg);
        }, (extractorArgument, extractor));

    public void ExtractContainerInformationFromMessage<TMessage>(Func<TMessage, IReadOnlyDictionary<string, string>, ContainerInformation> extractor) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromMessage<TMessage, Func<TMessage, IReadOnlyDictionary<string, string>, ContainerInformation>>((msg, headers, invoker) => invoker(msg, headers), extractor);

    public void ExtractContainerInformationFromMessage<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor,
        TArg extractorArgument)
    {
        if (extractContainerInformationFromMessagesTypes.Add(typeof(TMessage)))
        {
            ExtractContainerInformationFromMessage(new ContainerInformationFromMessageExtractor<TMessage, TArg>(extractor, extractorArgument));
        }
        else
        {
            throw new ArgumentException($"The message type '{typeof(TMessage).FullName}' is already being handled by a container message extractor and cannot be processed by another one.", nameof(TMessage));
        }
    }

    // Used by the Outbox to determine if it needs to try and extract the container from messages.
    internal bool HasCustomMessageExtractors => extractContainerInformationFromMessages.Count > 0;
    public void ExtractContainerInformationFromMessage(IContainerInformationFromMessagesExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);

        extractContainerInformationFromMessages.Add(extractor);
    }

    sealed class ContainerInformationFromMessageExtractor<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor, TArg argument = default)
        : IContainerInformationFromMessagesExtractor
    {
        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            if (message is TMessage typedMessage)
            {
                containerInformation = extractor(typedMessage, headers, argument);
                return true;
            }

            containerInformation = null;
            return false;
        }
    }
}