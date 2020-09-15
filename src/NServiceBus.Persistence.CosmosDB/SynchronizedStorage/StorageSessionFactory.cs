namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;

    class StorageSessionFactory : ISynchronizedStorage
    {
        readonly ContainerHolder containerHolder;

        public StorageSessionFactory(ContainerHolder containerHolder)
        {
            this.containerHolder = containerHolder;
        }

        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag) => Task.FromResult<CompletableSynchronizedStorageSession>(new StorageSession(containerHolder, contextBag, true));
    }
}