namespace NServiceBus
{
    using System;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// CosmosDB persistence specific extension methods for the <see cref="SynchronizedStorageSession"/>.
    /// </summary>
    public static class SynchronizedStorageSessionExtensions
    {
        /// <summary>
        /// Retrieves the current session from the context.
        /// </summary>
        public static ICosmosDBStorageSession GetCosmosDBStorageSession(this SynchronizedStorageSession session)
        {
            Guard.AgainstNull(nameof(session), session);

            if (session is IWorkWithSharedTransactionalBatch workWith)
            {
                return workWith.Create();
            }

            throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(CosmosDbPersistence)}>()' has been called.");
        }
    }
}