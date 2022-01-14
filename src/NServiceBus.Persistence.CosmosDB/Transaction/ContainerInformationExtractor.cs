namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;

    // TODO BOB: Unit tests
    // TODO BOB: Add acceptance test that mimics sample scenario
    class ContainerInformationExtractor : IContainerInformationFromHeadersExtractor, IContainerInformationFromMessagesExtractor
    {
        readonly HashSet<Type> extractContainerInformationFromMessagesTypes = new HashSet<Type>();

        readonly List<IContainerInformationFromMessagesExtractor> extractContainerInformationFromMessages =
            new List<IContainerInformationFromMessagesExtractor>();

        readonly HashSet<string> extractContainerInformationFromHeadersHeaderKeys = new HashSet<string>();

        readonly List<IContainerInformationFromHeadersExtractor> extractContainerInformationFromHeaders =
            new List<IContainerInformationFromHeadersExtractor>();

        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractContainerInformationFromHeaders.Count; index++)
            {
                var extractor = extractContainerInformationFromHeaders[index];
                if (extractor.TryExtract(headers, out containerInformation))
                {
                    return true;
                }
            }

            containerInformation = null;
            return false;
        }

        public void ExtractContainerInformationFromHeader(string headerKey, Func<string, ContainerInformation> converter) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractContainerInformationFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter);

        public void ExtractContainerInformationFromHeader<TArg>(string headerKey, Func<string, TArg, ContainerInformation> extractor, TArg converterArgument)
        {
            if (extractContainerInformationFromHeadersHeaderKeys.Add(headerKey))
            {
                ExtractContainerInformationFromHeaders(new ContainerInformationFromFromHeaderExtractor<TArg>(headerKey, extractor, converterArgument));
            }
            else
            {
                throw new ArgumentException($"The header key '{headerKey}' is already being handled by a container extractor and cannot be processed by another one.", nameof(headerKey));
            }
        }

        public void ExtractContainerInformationFromHeaders(IContainerInformationFromHeadersExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractContainerInformationFromHeaders.Add(extractor);
        }

        sealed class ContainerInformationFromFromHeaderExtractor<TArg> : IContainerInformationFromHeadersExtractor
        {
            readonly Func<string, TArg, ContainerInformation> extractor;
            readonly TArg extractorArgument;
            readonly string headerName;

            public ContainerInformationFromFromHeaderExtractor(string headerName, Func<string, TArg, ContainerInformation> extractor, TArg extractorArgument = default)
            {
                this.headerName = headerName;
                this.extractorArgument = extractorArgument;
                this.extractor = extractor;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    containerInformation = extractor(headerValue, extractorArgument);
                    return true;
                }
                containerInformation = null;
                return false;
            }
        }

        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractContainerInformationFromMessages.Count; index++)
            {
                var extractor = extractContainerInformationFromMessages[index];
                if (extractor.TryExtract(message, headers, out containerInformation))
                {
                    return true;
                }
            }

            containerInformation = null;
            return false;
        }

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
                throw new ArgumentException($"The message type '{typeof(TMessage).FullName}' is already being handled by a message extractor and cannot be processed by another one.", nameof(TMessage));
            }
        }

        public void ExtractContainerInformationFromMessage(IContainerInformationFromMessagesExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractContainerInformationFromMessages.Add(extractor);
        }

        sealed class ContainerInformationFromMessageExtractor<TMessage, TArg> : IContainerInformationFromMessagesExtractor
        {
            readonly Func<TMessage, IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor;
            readonly TArg argument;

            public ContainerInformationFromMessageExtractor(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor, TArg argument = default)
            {
                this.argument = argument;
                this.extractor = extractor;
            }

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
}