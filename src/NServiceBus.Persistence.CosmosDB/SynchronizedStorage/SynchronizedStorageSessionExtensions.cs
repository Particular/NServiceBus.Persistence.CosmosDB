namespace NServiceBus
{
    using System;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// Cosmos DB persistence specific extension methods for the <see cref="ISynchronizedStorageSession"/>.
    /// </summary>
    public static class SynchronizedStorageSessionExtensions
    {
        /// <summary>
        /// Retrieves the shared <see cref="ICosmosStorageSession"/> from the <see cref="ISynchronizedStorageSession"/>.
        /// </summary>
        public static ICosmosStorageSession CosmosPersistenceSession(this ISynchronizedStorageSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            if (session is IWorkWithSharedTransactionalBatch workWith)
            {
                return workWith.Create();
            }

            throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(CosmosPersistence)}>()' has been called.");
        }
    }
}