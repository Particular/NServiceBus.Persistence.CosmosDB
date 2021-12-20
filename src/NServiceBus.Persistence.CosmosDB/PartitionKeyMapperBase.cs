namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public abstract class PartitionKeyMapperBase
    {
        /// <summary>
        /// 
        /// </summary>
        protected IDictionary<Type, (Func<object, object, PartitionKey>, ContainerInformation?, object)> Extractors { get; } =
            new Dictionary<Type, (Func<object, object, PartitionKey>, ContainerInformation?, object)>();

        readonly Dictionary<string, IMapHeaders> headerMappers = new Dictionary<string, IMapHeaders>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        public bool TryMapMessage(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            => TryMapMessageCore(message, out partitionKey, out containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        protected virtual bool TryMapMessageCore(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
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
            => Extractors.Add(typeof(TMessage), ((msg, state) => ((Func<TMessage, PartitionKey>)state)((TMessage)msg), containerInformation, extractor));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        public bool TryMapHeader(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
            => TryMapHeaderCore(headers, out partitionKey, out containerInformation);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        protected virtual bool TryMapHeaderCore(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            partitionKey = null;
            containerInformation = null;
            foreach (var mapper in headerMappers.Values)
            {
                if (mapper.Map(headers, out partitionKey, out containerInformation))
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
            headerMappers.Add(headerName, new MapHeader<TState>(headerName, converter, containerInformation, state));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="containerInformation"></param>
        protected void ExtractFromHeader(string headerName, ContainerInformation? containerInformation = default) =>
            ExtractFromHeader<object>(headerName, (headerValue, _) => headerValue, containerInformation);

        interface IMapHeaders
        {
            bool Map(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
        }

        class MapHeader<TState> : IMapHeaders
        {
            readonly Func<string, TState, string> converter;
            readonly ContainerInformation? container;
            readonly TState state;
            readonly string headerName;

            public MapHeader(string headerName, Func<string, TState, string> converter, ContainerInformation? container, TState state = default)
            {
                this.headerName = headerName;
                this.state = state;
                this.container = container;
                this.converter = converter;
            }

            public bool Map(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
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