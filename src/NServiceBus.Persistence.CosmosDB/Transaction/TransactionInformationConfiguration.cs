namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// The configuration options for extracting transaction information.
    /// </summary>
    public class TransactionInformationConfiguration
    {
        /// <summary>
        /// Adds an instance of <see cref="IPartitionKeyFromHeadersExtractor"/> to the list of header extractors.
        /// </summary>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromHeaders(IPartitionKeyFromHeadersExtractor extractor) => PartitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor);

        /// <summary>
        /// Adds an instance of <see cref="IPartitionKeyFromMessagesExtractor"/> to the list of header extractors.
        /// </summary>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromMessages(IPartitionKeyFromMessagesExtractor extractor) => PartitionKeyExtractor.ExtractPartitionKeyFromMessages(extractor);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
        /// </summary>
        /// <param name="extractor">The extraction function.</param>
        /// <typeparam name="TMessage">The message type to match against.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor) =>
            PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
        /// </summary>
        /// <param name="extractor">The extraction function.</param>
        /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
        /// <typeparam name="TMessage">The message type to match against.</typeparam>
        /// <typeparam name="TArg">The argument type.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument) =>
            PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor, extractorArgument);


        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <param name="converter">The converter function to convert the header value.</param>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, string> converter) =>
            PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, converter);


        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <param name="converter">The converter function to convert the header value.</param>
        /// <param name="converterArgument">The argument passed as state to the <paramref name="converter"/></param>
        /// <typeparam name="TArg">The argument type.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter, TArg converterArgument) =>
            PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, converter, converterArgument);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractPartitionKeyFromHeader(string headerKey) =>
            PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey);

        internal PartitionKeyExtractor PartitionKeyExtractor { get; } = new PartitionKeyExtractor();
        internal ContainerInformationExtractor ContainerInformationExtractor { get; } = new ContainerInformationExtractor();
    }
}