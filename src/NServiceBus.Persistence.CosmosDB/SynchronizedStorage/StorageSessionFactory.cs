namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;

    class StorageSessionFactory : ISynchronizedStorage
    {
        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            var container = contextBag.Get<Container>();

            return Task.FromResult<CompletableSynchronizedStorageSession>(new StorageSession(container, true));
        }
    }
}