namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public abstract class PartitionKeyExtractorBase
    {
        readonly Dictionary<Type, IExtractPartitionKeyFromMessage> partitionKeyFromMessageExtractorsByTypeName =
            new Dictionary<Type, IExtractPartitionKeyFromMessage>();

        readonly Dictionary<string, IExtractPartitionKeyFromHeader> partitionKeyFromHeaderExtractorsByHeaderName =
            new Dictionary<string, IExtractPartitionKeyFromHeader>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        public bool TryExtractFromMessage(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            => TryExtractFromMessageCore(message, out partitionKey, out containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        protected virtual bool TryExtractFromMessageCore(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
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
        protected void ExtractFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor, ContainerInformation? containerInformation = default)
            // TODO: When moving to CSharp 9 these can be static lambdas
            => partitionKeyFromMessageExtractorsByTypeName.Add(typeof(TMessage), new ExtractPartitionKeyFromMessage<TMessage, Func<TMessage, PartitionKey>>((msg, invoker) => invoker(msg), containerInformation, extractor));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <param name="containerInformation"></param>
        /// <param name="state"></param>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TState"></typeparam>
        protected void ExtractFromMessage<TMessage, TState>(Func<TMessage, TState, PartitionKey> extractor, ContainerInformation? containerInformation = default, TState state = default)
            // TODO: Discuss if should not add but overwrite?
            => partitionKeyFromMessageExtractorsByTypeName.Add(typeof(TMessage), new ExtractPartitionKeyFromMessage<TMessage, TState>(extractor, containerInformation, state));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        public bool TryExtractFromHeader(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            => TryExtractFromHeaderCore(headers, out partitionKey, out containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        protected virtual bool TryExtractFromHeaderCore(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            partitionKey = null;
            containerInformation = null;
            foreach (var mapper in partitionKeyFromHeaderExtractorsByHeaderName.Values)
            {
                if (mapper.TryExtract(headers, out partitionKey, out containerInformation))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        protected void ExtractFromHeader(string headerName, Func<string, string> converter,
            ContainerInformation? containerInformation = default) =>
            // TODO: When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader(headerName, (headerValue, invoker) => invoker(headerValue), containerInformation, converter);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="converter"></param>
        /// <param name="containerInformation"></param>
        /// <param name="state"></param>
        /// <typeparam name="TState"></typeparam>
        protected void ExtractFromHeader<TState>(string headerName, Func<string, TState, string> converter,
            ContainerInformation? containerInformation = default, TState state = default) =>
            // TODO: Discuss if should not add but overwrite?
            partitionKeyFromHeaderExtractorsByHeaderName.Add(headerName, new ExtractPartitionKeyFromFromHeader<TState>(headerName, converter, containerInformation, state));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="containerInformation"></param>
        protected void ExtractFromHeader(string headerName, ContainerInformation? containerInformation = default) =>
            // TODO: When moving to CSharp 9 these can be static lambdas
            ExtractFromHeader<object>(headerName, (headerValue, _) => headerValue, containerInformation);

        interface IExtractPartitionKeyFromMessage
        {
            bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
        }

        sealed class ExtractPartitionKeyFromMessage<TMessage, TState> : IExtractPartitionKeyFromMessage
        {
            readonly Func<TMessage, TState, PartitionKey> extractor;
            readonly ContainerInformation? container;
            readonly TState state;

            public ExtractPartitionKeyFromMessage(Func<TMessage, TState, PartitionKey> extractor, ContainerInformation? container, TState state = default)
            {
                this.state = state;
                this.container = container;
                this.extractor = extractor;
            }

            public bool TryExtract(object message, out PartitionKey? partitionKey,
                out ContainerInformation? containerInformation)
            {
                partitionKey = null;
                containerInformation = null;
                if (message is TMessage typedMessage)
                {
                    partitionKey = extractor(typedMessage, state);
                    containerInformation = container;
                    return true;
                }
                return false;
            }
        }

        interface IExtractPartitionKeyFromHeader
        {
            bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
        }

        sealed class ExtractPartitionKeyFromFromHeader<TState> : IExtractPartitionKeyFromHeader
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
                partitionKey = null;
                containerInformation = null;
                if (headers.TryGetValue(headerName, out var headerValue))
                {
                    partitionKey = new PartitionKey(converter(headerValue, state));
                    containerInformation = container;
                    return true;
                }
                return false;
            }
        }
    }
}