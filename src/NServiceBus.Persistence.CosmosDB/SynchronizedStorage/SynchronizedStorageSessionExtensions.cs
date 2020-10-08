namespace NServiceBus
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// Cosmos DB persistence specific extension methods for the <see cref="SynchronizedStorageSession"/>.
    /// </summary>
    public static class SynchronizedStorageSessionExtensions
    {
        /// <summary>
        /// Retrieves the shared <see cref="ICosmosStorageSession"/> from the <see cref="SynchronizedStorageSession"/>.
        /// </summary>
        public static ICosmosStorageSession CosmosPersistenceSession(this SynchronizedStorageSession session)
        {
            Guard.AgainstNull(nameof(session), session);

            if (session is IWorkWithSharedTransactionalBatch workWith)
            {
                return workWith.Create();
            }

            throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(CosmosPersistence)}>()' has been called.");
        }
    }
}