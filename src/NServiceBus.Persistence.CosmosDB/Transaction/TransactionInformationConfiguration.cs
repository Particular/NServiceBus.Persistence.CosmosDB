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
        ///
        /// </summary>
        /// <param name="extractor"></param>
        public void ExtractFromHeaders(ITransactionInformationFromHeadersExtractor extractor) => Extractor.ExtractFromHeaders(extractor);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        public void ExtractFromMessages(ITransactionInformationFromMessagesExtractor extractor) => Extractor.ExtractFromMessages(extractor);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <typeparam name="TMessage"></typeparam>
        public void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromMessage(extractor, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <param name="extractorArgument"></param>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromMessage(extractor, extractorArgument, containerInformation);


        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromHeader(headerKey, converter, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        /// <param name="converterArgument"></param>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter, TArg converterArgument, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromHeader(headerKey, converter, converterArgument, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, ContainerInformation? containerInformation = default) =>
            Extractor.ExtractFromHeader(headerKey, containerInformation);

        internal TransactionInformationExtractor Extractor { get; } = new TransactionInformationExtractor();
    }
}