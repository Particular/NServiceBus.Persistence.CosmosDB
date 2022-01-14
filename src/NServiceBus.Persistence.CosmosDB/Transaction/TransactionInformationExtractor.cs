namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    class TransactionInformationExtractor : IPartitionKeyFromHeadersExtractor, IPartitionKeyFromMessagesExtractor
    {
        readonly HashSet<Type> extractPartitionKeyFromMessagesTypes = new HashSet<Type>();

        readonly List<IPartitionKeyFromMessagesExtractor> extractPartitionKeyFromMessages =
            new List<IPartitionKeyFromMessagesExtractor>();

        readonly HashSet<string> extractPartitionKeyFromHeadersHeaderKeys = new HashSet<string>();

        readonly List<IPartitionKeyFromHeadersExtractor> extractPartitionKeyFromHeaders =
            new List<IPartitionKeyFromHeadersExtractor>();

        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractPartitionKeyFromMessages.Count; index++)
            {
                var extractor = extractPartitionKeyFromMessages[index];
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
            ExtractPartitionKeyFromMessage<TMessage, (TArg, Func<TMessage, TArg, PartitionKey>)>((msg, _, args) =>
            {
                (TArg arg, Func<TMessage, TArg, PartitionKey> invoker) = args;
                return invoker(msg, arg);
            }, (extractorArgument, extractor));

        public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, IReadOnlyDictionary<string, string>, PartitionKey> extractor) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromMessage<TMessage, Func<TMessage, IReadOnlyDictionary<string, string>, PartitionKey>>((msg, headers, invoker) => invoker(msg, headers), extractor);

        public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor,
            TArg extractorArgument)
        {
            if (extractPartitionKeyFromMessagesTypes.Add(typeof(TMessage)))
            {
                ExtractFromMessages(new PartitionKeyFromMessageExtractor<TMessage, TArg>(extractor, extractorArgument));
            }
            else
            {
                throw new ArgumentException($"The message type '{typeof(TMessage).FullName}' is already being handled by a message extractor and cannot be processed by another one.", nameof(TMessage));
            }
        }

        public void ExtractFromMessages(IPartitionKeyFromMessagesExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractPartitionKeyFromMessages.Add(extractor);
        }

        sealed class PartitionKeyFromMessageExtractor<TMessage, TArg> : IPartitionKeyFromMessagesExtractor
        {
            readonly Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor;
            readonly TArg argument;

            public PartitionKeyFromMessageExtractor(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor, TArg argument = default)
            {
                this.argument = argument;
                this.extractor = extractor;
            }

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

        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractPartitionKeyFromHeaders.Count; index++)
            {
                var extractor = extractPartitionKeyFromHeaders[index];
                if (extractor.TryExtract(headers, out partitionKey))
                {
                    return true;
                }
            }

            partitionKey = null;
            return false;
        }

        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, string> converter) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter);

        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter,
            TArg converterArgument)
        {
            if (extractPartitionKeyFromHeadersHeaderKeys.Add(headerKey))
            {
                ExtractFromHeaders(new PartitionKeyFromFromHeaderExtractor<TArg>(headerKey, converter, converterArgument));
            }
            else
            {
                throw new ArgumentException($"The header key '{headerKey}' is already being handled by a header extractor and cannot be processed by another one.", nameof(headerKey));
            }
        }

        public void ExtractFromHeader(string headerKey) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader<object>(headerKey, (headerValue, _) => headerValue, null);

        public void ExtractFromHeaders(IPartitionKeyFromHeadersExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractPartitionKeyFromHeaders.Add(extractor);
        }

        sealed class PartitionKeyFromFromHeaderExtractor<TArg> : IPartitionKeyFromHeadersExtractor
        {
            readonly Func<string, TArg, string> converter;
            readonly TArg converterArgument;
            readonly string headerName;

            public PartitionKeyFromFromHeaderExtractor(string headerName, Func<string, TArg, string> converter, TArg converterArgument = default)
            {
                this.headerName = headerName;
                this.converterArgument = converterArgument;
                this.converter = converter;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = new PartitionKey(converter(headerValue, converterArgument));
                    return true;
                }
                partitionKey = null;
                return false;
            }
        }
    }
}