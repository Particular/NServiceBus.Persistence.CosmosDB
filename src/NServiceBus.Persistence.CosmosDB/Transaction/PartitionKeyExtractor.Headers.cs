namespace NServiceBus.Persistence.CosmosDB;

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
        for (int index = 0; index < extractPartitionKeyFromHeaders.Count; index++)
        {
            IPartitionKeyFromHeadersExtractor extractor = extractPartitionKeyFromHeaders[index];
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

    // Used by the Outbox to determine if it needs to try and extract the partition key from headers.
    internal bool HasCustomHeaderExtractors => extractPartitionKeyFromHeaders.Count > 0;

    sealed class PartitionKeyFromFromHeaderExtractor<TArg>(string headerName, Func<string, TArg, PartitionKey> extractor, TArg extractorArgument = default)
        : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            if (headers.TryGetValue(headerName, out string headerValue))
            {
                partitionKey = extractor(headerValue, extractorArgument);
                return true;
            }

            partitionKey = null;
            return false;
        }
    }

    sealed class PartitionKeyFromFromHeadersExtractor<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey?> extractor, TArg extractorArgument)
        : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = extractor.Invoke(headers, extractorArgument);
            return partitionKey != null;
        }
    }
}