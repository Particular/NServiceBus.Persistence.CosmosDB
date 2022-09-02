namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;

    class StorageSessionFactory : ISynchronizedStorage
    {
        public StorageSessionFactory(ContainerHolderResolver containerHolderResolver)
        {
            this.containerHolderResolver = containerHolderResolver;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            var storageSession = new StorageSession(containerHolderResolver, contextBag, true);
            return Task.FromResult<CompletableSynchronizedStorageSession>(storageSession);
        }

        readonly ContainerHolderResolver containerHolderResolver;
    }
}