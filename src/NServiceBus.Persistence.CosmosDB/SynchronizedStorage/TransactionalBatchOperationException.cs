namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Exception that is thrown when the transactional batch failed. The exception gives access to the
    /// <see cref="TransactionalBatchOperationResult"/> that exposes more details about the reason of failure.
    /// </summary>
    public sealed class TransactionalBatchOperationException : Exception
    {
        /// <summary>
        /// Initializes a new TransactionalBatchOperationException with a <see cref="TransactionalBatchOperationResult"/>.
        /// </summary>
        public TransactionalBatchOperationException(TransactionalBatchOperationResult result)
        {
            Result = result;
        }

        /// <summary>
        /// Initializes a new TransactionalBatchOperationException with a message and a <see cref="TransactionalBatchOperationResult"/>.
        /// </summary>
        public TransactionalBatchOperationException(string message, TransactionalBatchOperationResult result) : base(message)
        {
            Result = result;
        }

        /// <summary>
        /// The <see cref="TransactionalBatchOperationResult"/> exposing details about the reason of failure.
        /// </summary>
        public TransactionalBatchOperationResult Result { get; }
    }
}