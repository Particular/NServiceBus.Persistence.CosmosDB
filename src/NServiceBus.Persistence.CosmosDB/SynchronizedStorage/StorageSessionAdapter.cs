namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Outbox;
    using Outbox;
    using Transport;

    class StorageSessionAdapter : ISynchronizedStorageAdapter
    {
        public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
        {
            if (transaction is CosmosOutboxTransaction cosmosOutboxTransaction)
            {
                return Task.FromResult((CompletableSynchronizedStorageSession)cosmosOutboxTransaction.StorageSession);
            }

            return emptyResult;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context)
        {
            return emptyResult;
        }

        static readonly Task<CompletableSynchronizedStorageSession> emptyResult = Task.FromResult((CompletableSynchronizedStorageSession)null);
    }
}