namespace NServiceBus.Testing
{
    using Microsoft.Azure.Cosmos;
    using Extensibility;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// A fake implementation for <see cref="SynchronizedStorageSession"/> for testing purposes.
    /// </summary>
    public class TestableCosmosSynchronizedStorageSession : ISynchronizedStorageSession, IWorkWithSharedTransactionalBatch
    {
        /// <summary>
        /// Initializes a new TestableCosmosSynchronizedStorageSession with a partition key.
        /// </summary>
        public TestableCosmosSynchronizedStorageSession(PartitionKey partitionKey)
        {
            var contextBag = new ContextBag();
            contextBag.Set(partitionKey);
            ((IWorkWithSharedTransactionalBatch)this).CurrentContextBag = contextBag;
        }

        ContextBag IWorkWithSharedTransactionalBatch.CurrentContextBag { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Container Container { get; set; }

        /// <summary>
        ///
        /// </summary>
        public PartitionKeyPath PartitionKeyPath { get; set; }

        /// <summary>
        ///
        /// </summary>
        public TransactionalBatch TransactionalBatch { get; set; }

        void IWorkWithSharedTransactionalBatch.AddOperation(IOperation operation)
        {
            if (TransactionalBatch == null)
            {
                return;
            }
            operation.Apply(TransactionalBatch, PartitionKeyPath);
        }
    }
}