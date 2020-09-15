namespace NServiceBus.Testing
{
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Extensibility;
    using Persistence;
    using Persistence.CosmosDB;

    /// <summary>
    /// A fake implementation for <see cref="SynchronizedStorageSession"/> for testing purposes.
    /// </summary>
    public class TestableCosmosSynchronizedStorageSession : SynchronizedStorageSession, IWorkWithSharedTransactionalBatch
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="partitionKey"></param>
        public TestableCosmosSynchronizedStorageSession(PartitionKey partitionKey)
        {
            var contextBag = new ContextBag();
            contextBag.Set(partitionKey);
            ((IWorkWithSharedTransactionalBatch)this).CurrentContextBag = contextBag;
        }

        ContextBag IWorkWithSharedTransactionalBatch.CurrentContextBag { get; set; }

        void IWorkWithSharedTransactionalBatch.AddOperation(Operation operation)
        {
            //Do nothing (for now?)
        }
    }
}