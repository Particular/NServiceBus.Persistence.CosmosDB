namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;

    class StorageSessionFactory : ISynchronizedStorage
    {
        public StorageSessionFactory(ContainerHolder containerHolder, CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder)
        {
            this.currentSharedTransactionalBatchHolder = currentSharedTransactionalBatchHolder;
            this.containerHolder = containerHolder;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            var storageSession = new StorageSession(containerHolder, contextBag, true);
            currentSharedTransactionalBatchHolder?.SetCurrent(storageSession);
            return Task.FromResult<CompletableSynchronizedStorageSession>(storageSession);
        }

        readonly ContainerHolder containerHolder;
        readonly CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder;
    }
}