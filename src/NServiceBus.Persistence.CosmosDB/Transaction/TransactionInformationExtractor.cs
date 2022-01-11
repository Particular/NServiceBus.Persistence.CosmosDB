namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    class TransactionInformationExtractor : ITransactionInformationFromHeadersExtractor, ITransactionInformationFromMessagesExtractor
    {
        readonly HashSet<Type> extractTransactionInformationFromMessagesTypes = new HashSet<Type>();

        readonly List<ITransactionInformationFromMessagesExtractor> extractTransactionInformationFromMessages =
            new List<ITransactionInformationFromMessagesExtractor>();

        readonly HashSet<string> extractTransactionInformationFromHeadersHeaderKeys = new HashSet<string>();

        readonly List<ITransactionInformationFromHeadersExtractor> extractTransactionInformationFromHeaders =
            new List<ITransactionInformationFromHeadersExtractor>();

        public IReadOnlyCollection<ITransactionInformationFromHeadersExtractor> HeaderExtractors => extractTransactionInformationFromHeaders;

        public IReadOnlyCollection<ITransactionInformationFromMessagesExtractor> MessageExtractors => extractTransactionInformationFromMessages;

        /// <inheritdoc />
        bool ITransactionInformationFromMessagesExtractor.TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractTransactionInformationFromMessages.Count; index++)
            {
                var extractor = extractTransactionInformationFromMessages[index];
                if (extractor.TryExtract(message, out partitionKey, out containerInformation))
                {
                    return true;
                }
            }

            partitionKey = null;
            containerInformation = null;
            return false;
        }

        public void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractFromMessage<TMessage, Func<TMessage, PartitionKey>>((msg, invoker) => invoker(msg), extractor, containerInformation);

        public void ExtractFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor,
            TArg extractorArgument, ContainerInformation? containerInformation = default)
        {
            if (extractTransactionInformationFromMessagesTypes.Add(typeof(TMessage)))
            {
                extractTransactionInformationFromMessages.Add(new PartitionKeyFromMessageExtractor<TMessage, TArg>(extractor, containerInformation, extractorArgument));
            }
            else
            {
                throw new ArgumentException($"The message type '{typeof(TMessage).FullName}' is already being handled by a message extractor and cannot be processed by another one.", nameof(TMessage));
            }
        }

        public void ExtractFromMessages(ITransactionInformationFromMessagesExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractTransactionInformationFromMessages.Add(extractor);
        }

        sealed class PartitionKeyFromMessageExtractor<TMessage, TArg> : ITransactionInformationFromMessagesExtractor
        {
            readonly Func<TMessage, TArg, PartitionKey> extractor;
            readonly ContainerInformation? container;
            readonly TArg argument;

            public PartitionKeyFromMessageExtractor(Func<TMessage, TArg, PartitionKey> extractor, ContainerInformation? container,
                TArg argument = default)
            {
                this.argument = argument;
                this.container = container;
                this.extractor = extractor;
            }

            public bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (message is TMessage typedMessage)
                {
                    partitionKey = extractor(typedMessage, argument);
                    containerInformation = container;
                    return true;
                }
                partitionKey = null;
                containerInformation = null;
                return false;
            }
        }

        /// <inheritdoc />
        bool ITransactionInformationFromHeadersExtractor.TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractTransactionInformationFromHeaders.Count; index++)
            {
                var extractor = extractTransactionInformationFromHeaders[index];
                if (extractor.TryExtract(headers, out partitionKey, out containerInformation))
                {
                    return true;
                }
            }

            partitionKey = null;
            containerInformation = null;
            return false;
        }

        public void ExtractFromHeader(string headerKey, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter, containerInformation);

        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter,
            TArg converterArgument, ContainerInformation? containerInformation = default)
        {
            if (extractTransactionInformationFromHeadersHeaderKeys.Add(headerKey))
            {
                extractTransactionInformationFromHeaders.Add(new PartitionKeyFromFromHeaderExtractor<TArg>(headerKey, converter, containerInformation, converterArgument));
            }
            else
            {
                throw new ArgumentException($"The header key '{headerKey}' is already being handled by a header extractor and cannot be processed by another one.", nameof(headerKey));
            }
        }

        public void ExtractFromHeader(string headerKey, ContainerInformation? containerInformation = default) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader<object>(headerKey, (headerValue, _) => headerValue, null, containerInformation);

        public void ExtractFromHeaders(ITransactionInformationFromHeadersExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractTransactionInformationFromHeaders.Add(extractor);
        }

        sealed class PartitionKeyFromFromHeaderExtractor<TArg> : ITransactionInformationFromHeadersExtractor
        {
            readonly Func<string, TArg, string> converter;
            readonly ContainerInformation? container;
            readonly TArg converterArgument;
            readonly string headerName;

            public PartitionKeyFromFromHeaderExtractor(string headerName, Func<string, TArg, string> converter, ContainerInformation? container, TArg converterArgument = default)
            {
                this.headerName = headerName;
                this.converterArgument = converterArgument;
                this.container = container;
                this.converter = converter;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = new PartitionKey(converter(headerValue, converterArgument));
                    containerInformation = container;
                    return true;
                }
                partitionKey = null;
                containerInformation = null;
                return false;
            }
        }
    }
}