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

        /// <summary>
        /// 
        /// </summary>
        protected IDictionary<string, (Func<string, string>, ContainerInformation?)> HeaderKeys { get; } =
            new Dictionary<string, (Func<string, string>, ContainerInformation?)>();

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
            foreach (var headerKey in HeaderKeys)
            {
                if (headers.TryGetValue(headerKey.Key, out var headerValue))
                {
                    var (invoker, container) = headerKey.Value;
                    partitionKey = new PartitionKey(invoker(headerValue));
                    containerInformation = container;
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
            // TODO: Discuss if should not add but overwrite?
            HeaderKeys.Add(headerName, (converter, containerInformation));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="containerInformation"></param>
        protected void ExtractFromHeader(string headerName, ContainerInformation? containerInformation = default) =>
            // TODO: Discuss if should not add but overwrite?
            HeaderKeys.Add(headerName, (value => value, containerInformation));
    }
}