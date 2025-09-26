namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;

// The overloads with the extractor argument state are there to enable low allocation scenarios (avoiding closure allocations)
partial class ContainerInformationExtractor : IContainerInformationFromHeadersExtractor, IContainerInformationFromMessagesExtractor
{
    readonly HashSet<string> extractContainerInformationFromHeadersHeaderKeys = [];

    readonly List<IContainerInformationFromHeadersExtractor> extractContainerInformationFromHeaders =
        [];

    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        // deliberate use of a for loop
        for (int index = 0; index < extractContainerInformationFromHeaders.Count; index++)
        {
            IContainerInformationFromHeadersExtractor extractor = extractContainerInformationFromHeaders[index];
            if (extractor.TryExtract(headers, out containerInformation))
            {
                return true;
            }
        }

        containerInformation = null;
        return false;
    }

    // Used by the Outbox to determine if it needs to try and extract the container from headers.
    internal bool HasCustomHeaderExtractors => extractContainerInformationFromHeaders.Count > 0;

    public void ExtractContainerInformationFromHeader(string headerKey, ContainerInformation containerInformation) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromHeader(headerKey, (_, container) => container, containerInformation);

    public void ExtractContainerInformationFromHeader(string headerKey, Func<string, ContainerInformation> converter) =>
        // When moving to CSharp 9 these can be static lambdas
        ExtractContainerInformationFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter);

    public void ExtractContainerInformationFromHeader<TArg>(string headerKey, Func<string, TArg, ContainerInformation> extractor, TArg extractorArgument)
    {
        if (extractContainerInformationFromHeadersHeaderKeys.Add(headerKey))
        {
            ExtractContainerInformationFromHeaders(new ContainerInformationFromFromHeaderExtractor<TArg>(headerKey, extractor, extractorArgument));
        }
        else
        {
            throw new ArgumentException($"The header key '{headerKey}' is already being handled by a container header extractor and cannot be processed by another one.", nameof(headerKey));
        }
    }

    public void ExtractContainerInformationFromHeaders(Func<IReadOnlyDictionary<string, string>, ContainerInformation?> extractor)
        // When moving to CSharp 9 these can be static lambdas
        => ExtractContainerInformationFromHeaders(new ContainerInformationFromHeadersExtractor<Func<IReadOnlyDictionary<string, string>, ContainerInformation?>>(
            (headers, invoker) => invoker(headers), extractor));

    public void ExtractContainerInformationFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation?> extractor, TArg extractorArgument)
        => ExtractContainerInformationFromHeaders(new ContainerInformationFromHeadersExtractor<TArg>(extractor, extractorArgument));

    public void ExtractContainerInformationFromHeaders(IContainerInformationFromHeadersExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);

        extractContainerInformationFromHeaders.Add(extractor);
    }

    sealed class ContainerInformationFromFromHeaderExtractor<TArg>(string headerName, Func<string, TArg, ContainerInformation> extractor, TArg extractorArgument = default)
        : IContainerInformationFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            if (headers.TryGetValue(headerName, out string headerValue))
            {
                containerInformation = extractor(headerValue, extractorArgument);
                return true;
            }

            containerInformation = null;
            return false;
        }
    }

    sealed class ContainerInformationFromHeadersExtractor<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation?> extractor, TArg extractorArgument)
        : IContainerInformationFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            containerInformation = extractor.Invoke(headers, extractorArgument);
            return containerInformation != null;
        }
    }
}