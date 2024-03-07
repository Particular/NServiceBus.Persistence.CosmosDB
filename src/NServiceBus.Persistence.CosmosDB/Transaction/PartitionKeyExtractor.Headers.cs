namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    // The overloads with the extractor argument state are there to enable low allocation scenarios (avoiding closure allocations)
    partial class PartitionKeyExtractor : IPartitionKeyFromHeadersExtractor, IPartitionKeyFromMessageExtractor
    {
        readonly HashSet<string> extractPartitionKeyFromHeadersHeaderKeys = [];

        readonly List<IPartitionKeyFromHeadersExtractor> extractPartitionKeyFromHeaders =
            [];

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

        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, string> extractor) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, invoker) => new PartitionKey(invoker(headerValue)), extractor);

        public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, string> extractor, TArg extractorArgument) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, args) =>
            {
                (Func<string, TArg, string> invoker, TArg arg) = args;
                return new PartitionKey(invoker(headerValue, arg));
            }, (extractor, extractorArgument));

        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, PartitionKey> extractor) =>
            // When moving to CSharp 9 these can be static lambdas
            ExtractPartitionKeyFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), extractor);

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

        public void ExtractPartitionKeyFromHeaders(Func<IReadOnlyDictionary<string, string>, PartitionKey?> extractor)
            // When moving to CSharp 9 these can be static lambdas
            => ExtractPartitionKeyFromHeaders(new PartitionKeyFromFromHeadersExtractor<Func<IReadOnlyDictionary<string, string>, PartitionKey?>>(
                (headers, invoker) => invoker(headers), extractor));

        public void ExtractPartitionKeyFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey?> extractor, TArg extractorArgument)
            => ExtractPartitionKeyFromHeaders(new PartitionKeyFromFromHeadersExtractor<TArg>(extractor, extractorArgument));

        public void ExtractPartitionKeyFromHeaders(IPartitionKeyFromHeadersExtractor extractor)
        {
            ArgumentNullException.ThrowIfNull(extractor);

            extractPartitionKeyFromHeaders.Add(extractor);
        }

        sealed class PartitionKeyFromFromHeaderExtractor<TArg> : IPartitionKeyFromHeadersExtractor
        {
            readonly Func<string, TArg, PartitionKey> extractor;
            readonly TArg extractorArgument;
            readonly string headerName;

            public PartitionKeyFromFromHeaderExtractor(string headerName, Func<string, TArg, PartitionKey> extractor, TArg extractorArgument = default)
            {
                this.headerName = headerName;
                this.extractorArgument = extractorArgument;
                this.extractor = extractor;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = extractor(headerValue, extractorArgument);
                    return true;
                }
                partitionKey = null;
                return false;
            }
        }

        sealed class PartitionKeyFromFromHeadersExtractor<TArg> : IPartitionKeyFromHeadersExtractor
        {
            readonly Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey?> extractor;
            readonly TArg extractorArgument;

            public PartitionKeyFromFromHeadersExtractor(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey?> extractor, TArg extractorArgument)
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