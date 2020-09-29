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
        public TransactionalBatchOperationException(TransactionalBatchOperationResult result)
        {
            Result = result;
        }

        /// <summary>
        ///
        /// </summary>
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