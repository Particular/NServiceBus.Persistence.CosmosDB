namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;

    class StorageSessionFactory : ISynchronizedStorage
    {
        public StorageSessionFactory(ContainerHolderResolver containerHolderResolver, CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder)
        {
            this.containerHolderResolver = containerHolderResolver;
            this.currentSharedTransactionalBatchHolder = currentSharedTransactionalBatchHolder;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            var storageSession = new StorageSession(containerHolderResolver, contextBag, true);
            currentSharedTransactionalBatchHolder?.SetCurrent(storageSession);
            return Task.FromResult<CompletableSynchronizedStorageSession>(storageSession);
        }

        readonly CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder;
        readonly ContainerHolderResolver containerHolderResolver;
    }
}