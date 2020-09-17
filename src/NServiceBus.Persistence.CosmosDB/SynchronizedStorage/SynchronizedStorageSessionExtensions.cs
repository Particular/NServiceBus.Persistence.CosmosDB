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
        /// Retrieves the shared <see cref="TransactionalBatch"/> from the <see cref="SynchronizedStorageSession"/>.
        /// </summary>
        public static TransactionalBatch GetSharedTransactionalBatch(this SynchronizedStorageSession session)
        {
            Guard.AgainstNull(nameof(session), session);

            if (session is IWorkWithSharedTransactionalBatch workWith)
            {
                return workWith.Create().Batch;
            }

            throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(CosmosDbPersistence)}>()' has been called.");
        }
    }
}