namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public class TransactionInformationExtractor : IExtractTransactionInformationFromHeaders, IExtractTransactionInformationFromMessages
    {
        readonly HashSet<Type> extractTransactionInformationFromMessagesTypes = new HashSet<Type>();

        readonly List<IExtractTransactionInformationFromMessages> extractTransactionInformationFromMessages =
            new List<IExtractTransactionInformationFromMessages>();

        readonly HashSet<string> extractTransactionInformationFromHeadersHeaderKeys = new HashSet<string>();

        readonly List<IExtractTransactionInformationFromHeaders> extractTransactionInformationFromHeaders =
            new List<IExtractTransactionInformationFromHeaders>();

        /// <inheritdoc />
        bool IExtractTransactionInformationFromMessages.TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractTransactionInformationFromMessages.Count; index++)
            {
                var extractor = extractTransactionInformationFromMessages[index];
                if (extractor.TryExtract(message, out partitionKey, out containerInformation))
                {
                    return true;
                }
            }

            partitionKey = null;
            containerInformation = null;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <typeparam name="TMessage"></typeparam>
        public void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default) =>
            // TODO: When moving to CSharp 9 these can be static lambdas
            ExtractFromMessage<TMessage, Func<TMessage, PartitionKey>>((msg, invoker) => invoker(msg), extractor, containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <param name="extractorArgument"></param>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor,
            TArg extractorArgument, ContainerInformation? containerInformation = default)
        {
            if (extractTransactionInformationFromMessagesTypes.Add(typeof(TMessage)))
            {
                extractTransactionInformationFromMessages.Add(new ExtractPartitionKeyFromMessage<TMessage, TArg>(extractor, containerInformation, extractorArgument));
            }
            // TODO: Decide what to do in the else case
        }

        sealed class ExtractPartitionKeyFromMessage<TMessage, TArg> : IExtractTransactionInformationFromMessages
        {
            readonly Func<TMessage, TArg, PartitionKey> extractor;
            readonly ContainerInformation? container;
            readonly TArg argument;

            public ExtractPartitionKeyFromMessage(Func<TMessage, TArg, PartitionKey> extractor, ContainerInformation? container,
                TArg argument = default)
            {
                this.argument = argument;
                this.container = container;
                this.extractor = extractor;
            }

            public bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (message is TMessage typedMessage)
                {
                    partitionKey = extractor(typedMessage, argument);
                    containerInformation = container;
                    return true;
                }
                partitionKey = null;
                containerInformation = null;
                return false;
            }
        }

        /// <inheritdoc />
        bool IExtractTransactionInformationFromHeaders.TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            // deliberate use of a for loop
            for (var index = 0; index < extractTransactionInformationFromHeaders.Count; index++)
            {
                var extractor = extractTransactionInformationFromHeaders[index];
                if (extractor.TryExtract(headers, out partitionKey, out containerInformation))
                {
                    return true;
                }
            }

            partitionKey = null;
            containerInformation = null;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            // TODO: When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), converter, containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        /// <param name="converterArgument"></param>
        /// <typeparam name="TArg"></typeparam>
        public void ExtractFromHeader<TArg>(string headerKey, Func<string, TArg, string> converter,
            TArg converterArgument, ContainerInformation? containerInformation = default)
        {
            if (extractTransactionInformationFromHeadersHeaderKeys.Add(headerKey))
            {
                extractTransactionInformationFromHeaders.Add(new ExtractPartitionKeyFromFromHeader<TArg>(headerKey, converter, containerInformation, converterArgument));
            }
            // TODO: Decide what to do in the else case
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="containerInformation"></param>
        public void ExtractFromHeader(string headerKey, ContainerInformation? containerInformation = default) =>
            // TODO: When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader<object>(headerKey, (headerValue, _) => headerValue, null, containerInformation);

        sealed class ExtractPartitionKeyFromFromHeader<TArg> : IExtractTransactionInformationFromHeaders
        {
            readonly Func<string, TArg, string> converter;
            readonly ContainerInformation? container;
            readonly TArg _converterArgument;
            readonly string headerName;

            public ExtractPartitionKeyFromFromHeader(string headerName, Func<string, TArg, string> converter, ContainerInformation? container, TArg converterArgument = default)
            {
                this.headerName = headerName;
                this._converterArgument = converterArgument;
                this.container = container;
                this.converter = converter;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = new PartitionKey(converter(headerValue, _converterArgument));
                    containerInformation = container;
                    return true;
                }
                partitionKey = null;
                containerInformation = null;
                return false;
            }
        }
    }
}