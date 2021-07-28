﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Transport;

    class StorageSessionAdapter : ISynchronizedStorageAdapter
    {
        public StorageSessionAdapter(CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder)
        {
            this.currentSharedTransactionalBatchHolder = currentSharedTransactionalBatchHolder;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            if (transaction is CosmosOutboxTransaction cosmosOutboxTransaction)
            {
                cosmosOutboxTransaction.StorageSession.CurrentContextBag = context;
                currentSharedTransactionalBatchHolder?.SetCurrent(cosmosOutboxTransaction.StorageSession);
                return Task.FromResult((CompletableSynchronizedStorageSession)cosmosOutboxTransaction.StorageSession);
            }

            return emptyResult;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            return emptyResult;
        }

        static readonly Task<CompletableSynchronizedStorageSession> emptyResult = Task.FromResult((CompletableSynchronizedStorageSession)null);
        readonly CurrentSharedTransactionalBatchHolder currentSharedTransactionalBatchHolder;
    }
}