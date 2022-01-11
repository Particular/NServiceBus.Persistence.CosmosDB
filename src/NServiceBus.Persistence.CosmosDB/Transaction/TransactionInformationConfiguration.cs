namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// The configuration options for extracting transaction information.
    /// </summary>
    public class TransactionInformationConfiguration
    {
        readonly List<ITransactionInformationFromHeadersExtractor> headerExtractors =
            new List<ITransactionInformationFromHeadersExtractor>();

        readonly List<ITransactionInformationFromMessagesExtractor> messageExtractors =
            new List<ITransactionInformationFromMessagesExtractor>();

        TransactionInformationExtractor transactionInformationExtractor;

        // the extractor is separate to better test it and make it possible to faster execute those mappings since there we can directly use
        // directly the internal collection type while in the behavior we are based on enumerable.
        TransactionInformationExtractor TransactionInformationExtractor
        {
            get
            {
                if (transactionInformationExtractor == null)
                {
                    transactionInformationExtractor = new TransactionInformationExtractor();
                    headerExtractors.Add(transactionInformationExtractor);
                    messageExtractors.Add(transactionInformationExtractor);
                }

                return transactionInformationExtractor;
            }
        }


        /// <summary>
        /// Gets the registered header extractors.
        /// </summary>
        public IReadOnlyCollection<ITransactionInformationFromHeadersExtractor> HeaderExtractors => headerExtractors;

        /// <summary>
        /// Gets the registered message extractors.
        /// </summary>
        public IReadOnlyCollection<ITransactionInformationFromMessagesExtractor> MessageExtractors => messageExtractors;

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        public void ExtractFromHeaders(ITransactionInformationFromHeadersExtractor extractor) => headerExtractors.Add(extractor);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        public void ExtractFromMessages(ITransactionInformationFromMessagesExtractor extractor) => messageExtractors.Add(extractor);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <typeparam name="TMessage"></typeparam>
        public void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default) =>
            TransactionInformationExtractor.ExtractFromMessage(extractor, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <param name="extractorArgument"></param>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument, ContainerInformation? containerInformation = default) =>
            TransactionInformationExtractor.ExtractFromMessage(extractor, extractorArgument, containerInformation);


        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            TransactionInformationExtractor.ExtractFromHeader(headerKey, converter, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        /// <param name="converterArgument"></param>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter, TArg converterArgument, ContainerInformation? containerInformation = default) =>
            TransactionInformationExtractor.ExtractFromHeader(headerKey, converter, converterArgument, containerInformation);

        /// <summary>
        ///
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, ContainerInformation? containerInformation = default) =>
            TransactionInformationExtractor.ExtractFromHeader(headerKey, containerInformation);
    }
}