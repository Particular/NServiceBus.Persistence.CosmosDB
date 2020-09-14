namespace NServiceBus
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// CosmosDB persistence specific extension methods for the <see cref="SynchronizedStorageSession"/>.
    /// </summary>
    public static class SynchronizedStorageSessionExtensions
    {
        /// <summary>
        /// Retrieves the current transactional batch from the context.
        /// </summary>
        public static TransactionalBatch GetTransactionalBatch(this SynchronizedStorageSession session)
        {
            Guard.AgainstNull(nameof(session), session);

            if (session is ITransactionalBatchProvider storageSession)
            {
                return storageSession.TransactionalBatch;
            }

            throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(CosmosDbPersistence)}>()' has been called.");
        }
    }
}