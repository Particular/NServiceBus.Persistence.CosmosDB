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
        /// Adds an instance of <see cref="ITransactionInformationFromHeadersExtractor"/> to the list of header extractors.
        /// </summary>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromHeaders(ITransactionInformationFromHeadersExtractor extractor) => Extractor.ExtractFromHeaders(extractor);

        /// <summary>
        /// Adds an instance of <see cref="ITransactionInformationFromMessagesExtractor"/> to the list of header extractors.
        /// </summary>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromMessages(ITransactionInformationFromMessagesExtractor extractor) => Extractor.ExtractFromMessages(extractor);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
        /// </summary>
        /// <param name="extractor">The extraction function.</param>
        /// <param name="containerInformation">The optional container information to be used when this rule matches.</param>
        /// <typeparam name="TMessage">The message type to match against.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractPartitionKeyFromMessage(extractor);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
        /// </summary>
        /// <param name="extractor">The extraction function.</param>
        /// <param name="containerInformation">The optional container information to be used when this rule matches.</param>
        /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
        /// <typeparam name="TMessage">The message type to match against.</typeparam>
        /// <typeparam name="TArg">The argument type.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractPartitionKeyFromMessage(extractor, extractorArgument);


        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <param name="converter">The converter function to convert the header value.</param>
        /// <param name="containerInformation">The optional container information to be used when this rule matches.</param>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromHeader(string headerKey, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            Extractor.ExtractPartitionKeyFromHeader(headerKey, converter);


        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <param name="converter">The converter function to convert the header value.</param>
        /// <param name="containerInformation">The optional container information to be used when this rule matches.</param>
        /// <param name="converterArgument">The argument passed as state to the <paramref name="converter"/></param>
        /// <typeparam name="TArg">The argument type.</typeparam>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter, TArg converterArgument, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromHeader(headerKey, converter, converterArgument);

        /// <summary>
        /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
        /// </summary>
        /// <param name="headerKey">The header key.</param>
        /// <param name="containerInformation">The optional container information to be used when this rule matches.</param>
        /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
        public void ExtractFromHeader(string headerKey, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromHeader(headerKey);

        internal TransactionInformationExtractor Extractor { get; } = new TransactionInformationExtractor();
    }
}