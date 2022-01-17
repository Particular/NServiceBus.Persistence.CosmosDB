namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    partial class PartitionKeyExtractor : IPartitionKeyFromHeadersExtractor, IPartitionKeyFromMessageExtractor
    {
        readonly HashSet<string> extractPartitionKeyFromHeadersHeaderKeys = new HashSet<string>();

        readonly List<IPartitionKeyFromHeadersExtractor> extractPartitionKeyFromHeaders =
            new List<IPartitionKeyFromHeadersExtractor>();

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

        public void ExtractPartitionKeyFromHeader(string headerKey) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader<object>(headerKey, (headerValue, _) => new PartitionKey(headerValue), null);

        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, string> converter) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, invoker) => new PartitionKey(invoker(headerValue)), converter);

        public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, string> extractor, TArg extractorArgument) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, args) =>
            {
                (Func<string, TArg, string> invoker, TArg arg) = args;
                return new PartitionKey(invoker(headerValue, arg));
            }, (extractor, extractorArgument));

        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, PartitionKey> converter) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter);

        public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, PartitionKey> extractor, TArg extractorArgument)
        {
            if (extractPartitionKeyFromHeadersHeaderKeys.Add(headerKey))
            {
                ExtractPartitionKeyFromHeaders(new PartitionKeyFromFromHeaderExtractor<TArg>(headerKey, extractor, extractorArgument));
            }
            else
            {
                throw new ArgumentException($"The header key '{headerKey}' is already being handled by a header extractor and cannot be processed by another one.", nameof(headerKey));
            }
        }

        public void ExtractPartitionKeyFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor, TArg extractorArgument)
            => ExtractPartitionKeyFromHeaders(new PartitionKeyFromFromHeadersExtractor<TArg>(extractor, extractorArgument));

        public void ExtractPartitionKeyFromHeaders(IPartitionKeyFromHeadersExtractor extractor)
        {
            Guard.AgainstNull(nameof(extractor), extractor);

            extractPartitionKeyFromHeaders.Add(extractor);
        }

        sealed class PartitionKeyFromFromHeaderExtractor<TArg> : IPartitionKeyFromHeadersExtractor
        {
            readonly Func<string, TArg, PartitionKey> converter;
            readonly TArg converterArgument;
            readonly string headerName;

            public PartitionKeyFromFromHeaderExtractor(string headerName, Func<string, TArg, PartitionKey> converter, TArg converterArgument = default)
            {
                this.headerName = headerName;
                this.converterArgument = converterArgument;
                this.converter = converter;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = converter(headerValue, converterArgument);
                    return true;
                }
                partitionKey = null;
                return false;
            }
        }

        sealed class PartitionKeyFromFromHeadersExtractor<TArg> : IPartitionKeyFromHeadersExtractor
        {
            readonly Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor;
            readonly TArg extractorArgument;

            public PartitionKeyFromFromHeadersExtractor(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor, TArg extractorArgument)
            {
                this.extractor = extractor;
                this.extractorArgument = extractorArgument;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                partitionKey = extractor.Invoke(headers, extractorArgument);
                return partitionKey != null;
            }
        }
    }
}