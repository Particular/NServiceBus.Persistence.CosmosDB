namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;

    // TODO BOB: Add acceptance test that mimics sample scenario
    partial class ContainerInformationExtractor : IContainerInformationFromHeadersExtractor, IContainerInformationFromMessagesExtractor
    {
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

        public void ExtractContainerInformationFromHeaders(Func<IReadOnlyDictionary<string, string>, ContainerInformation> extractor)
            // When moving to CSharp 9 these can be static lambdas
            => ExtractContainerInformationFromHeaders(new ContainerInformationFromHeadersExtractor<Func<IReadOnlyDictionary<string, string>, ContainerInformation>>(
                (headers, invoker) => invoker(headers), extractor));

        public void ExtractContainerInformationFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor, TArg extractorArgument)
            => ExtractContainerInformationFromHeaders(new ContainerInformationFromHeadersExtractor<TArg>(extractor, extractorArgument));

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

        sealed class ContainerInformationFromHeadersExtractor<TArg> : IContainerInformationFromHeadersExtractor
        {
            readonly Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor;
            readonly TArg extractorArgument;

            public ContainerInformationFromHeadersExtractor(Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor, TArg extractorArgument)
            {
                this.extractor = extractor;
                this.extractorArgument = extractorArgument;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = extractor.Invoke(headers, extractorArgument);
                return containerInformation != null;
            }
        }
    }
}