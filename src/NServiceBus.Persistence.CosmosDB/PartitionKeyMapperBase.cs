namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public abstract class PartitionKeyMapperBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public bool TryMap(object message, out PartitionKey? partitionKey)
        {
            return TryMapCore(message, out partitionKey);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        protected virtual bool TryMapCore(object message, out PartitionKey? partitionKey)
        {
            partitionKey = null;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <typeparam name="TMessage"></typeparam>
        protected void ExtractFromMessage<TMessage>(Expression<Func<TMessage, Guid>> extractor)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extractor"></param>
        /// <typeparam name="TMessage"></typeparam>
        protected void ExtractFromMessage<TMessage>(Expression<Func<TMessage, string>> extractor)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerName"></param>
        protected void ExtractFromHeader(string headerName)
        {
        }
    }
}