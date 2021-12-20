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
            ExtractFromMessage<TMessage, Func<TMessage, PartitionKey>>((msg, invoker) => invoker(msg), containerInformation, extractor);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <param name="state"></param>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TState"></typeparam>
        public void ExtractFromMessage<TMessage, TState>(Func<TMessage, TState, PartitionKey> extractor,
            ContainerInformation? containerInformation = default, TState state = default)
        {
            if (extractTransactionInformationFromMessagesTypes.Add(typeof(TMessage)))
            {
                extractTransactionInformationFromMessages.Add(new ExtractPartitionKeyFromMessage<TMessage, TState>(extractor, containerInformation, state));
            }
            // TODO: Decide what to do in the else case
        }

        sealed class ExtractPartitionKeyFromMessage<TMessage, TState> : IExtractTransactionInformationFromMessages
        {
            readonly Func<TMessage, TState, PartitionKey> extractor;
            readonly ContainerInformation? container;
            readonly TState state;

            public ExtractPartitionKeyFromMessage(Func<TMessage, TState, PartitionKey> extractor, ContainerInformation? container,
                TState state = default)
            {
                this.state = state;
                this.container = container;
                this.extractor = extractor;
            }

            public bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (message is TMessage typedMessage)
                {
                    partitionKey = extractor(typedMessage, state);
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
            ExtractFromHeader(headerKey, (headerValue, invoker) => invoker(headerValue), containerInformation, converter);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerKey"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        /// <param name="state"></param>
        /// <typeparam name="TState"></typeparam>
        public void ExtractFromHeader<TState>(string headerKey, Func<string, TState, string> converter,
            ContainerInformation? containerInformation = default, TState state = default)
        {
            if (extractTransactionInformationFromHeadersHeaderKeys.Add(headerKey))
            {
                extractTransactionInformationFromHeaders.Add(new ExtractPartitionKeyFromFromHeader<TState>(headerKey, converter, containerInformation, state));
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
            ExtractFromHeader<object>(headerKey, (headerValue, _) => headerValue, containerInformation);

        sealed class ExtractPartitionKeyFromFromHeader<TState> : IExtractTransactionInformationFromHeaders
        {
            readonly Func<string, TState, string> converter;
            readonly ContainerInformation? container;
            readonly TState state;
            readonly string headerName;

            public ExtractPartitionKeyFromFromHeader(string headerName, Func<string, TState, string> converter, ContainerInformation? container, TState state = default)
            {
                this.headerName = headerName;
                this.state = state;
                this.container = container;
                this.converter = converter;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            {
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = new PartitionKey(converter(headerValue, state));
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