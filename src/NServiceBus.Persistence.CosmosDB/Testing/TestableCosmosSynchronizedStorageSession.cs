namespace NServiceBus.Testing
{
    using Microsoft.Azure.Cosmos;
    using Persistence;

    /// <summary>
    /// A fake implementation for <see cref="SynchronizedStorageSession"/> for testing purposes.
    /// </summary>
    public class TestableCosmosSynchronizedStorageSession : SynchronizedStorageSession
    {
        /// <summary>
        /// Creates a new instance of <see cref="TestableCosmosSynchronizedStorageSession"/> using the provided <see cref="TransactionalBatch"/>.
        /// </summary>
        public TestableCosmosSynchronizedStorageSession(TransactionalBatch transactionalBatch)
        {
            TransactionalBatch = transactionalBatch;
        }

        /// <summary>
        /// The transactional batch which is retrieved by calling <see cref="SynchronizedStorageSessionExtensions.GetTransactionalBatch"/>.
        /// </summary>
        public TransactionalBatch TransactionalBatch { get; }
    }
}