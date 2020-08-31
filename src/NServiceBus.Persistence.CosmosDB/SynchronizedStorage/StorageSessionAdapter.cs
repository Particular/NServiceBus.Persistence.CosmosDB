namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Transport;

    class StorageSessionAdapter : ISynchronizedStorageAdapter
    {
        public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
        {
            return emptyResult;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context)
        {
            return emptyResult;
        }

        static readonly Task<CompletableSynchronizedStorageSession> emptyResult = Task.FromResult((CompletableSynchronizedStorageSession)null);
    }
}