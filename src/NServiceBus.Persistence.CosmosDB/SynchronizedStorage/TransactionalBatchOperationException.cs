namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    public class TransactionalBatchOperationException : Exception
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="result"></param>
        public TransactionalBatchOperationException(TransactionalBatchOperationResult result)
        {
            Result = result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="result"></param>
        public TransactionalBatchOperationException(string message, TransactionalBatchOperationResult result) : base(message)
        {
            Result = result;
        }

        /// <summary>
        ///
        /// </summary>
        public TransactionalBatchOperationResult Result { get; }
    }
}