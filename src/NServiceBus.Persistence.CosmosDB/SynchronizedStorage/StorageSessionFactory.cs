namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    class StorageSessionFactory : ISynchronizedStorage
    {
        public StorageSessionFactory(ContainerHolderResolver containerHolderResolver, CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder)
        {
            this.containerHolderResolver = containerHolderResolver;
            this.currentSharedTransactionalBatchHolder = currentSharedTransactionalBatchHolder;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag, CancellationToken cancellationToken = default)
        {
            var storageSession = new StorageSession(containerHolderResolver, contextBag, true);

            //The null-conditional is to allow the PersistenceTests to run.
            //CurrentSharedTransactionalBatchBehavior.Invoke calls CreateScope which is needed for SetCurrent to work.
            //This is a workaround since PersistenceTests do not execute behaviors.
            currentSharedTransactionalBatchHolder?.SetCurrent(storageSession);
            return Task.FromResult<CompletableSynchronizedStorageSession>(storageSession);
        }

        readonly CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder;
        readonly ContainerHolderResolver containerHolderResolver;
    }
}