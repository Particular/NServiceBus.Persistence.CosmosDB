namespace NServiceBus.Persistence.CosmosDB.SynchronizedStorage
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Transport;

    class CosmosDbSynchronizedStorageSession : ICompletableSynchronizedStorageSession
    {
        readonly ContainerHolderResolver containerHolderResolver;
        bool commitOnComplete;
        bool disposed;

        public StorageSession StorageSession { get; private set; }

        public CosmosDbSynchronizedStorageSession(ContainerHolderResolver containerHolderResolver)
        {
            this.containerHolderResolver = containerHolderResolver;
        }

        public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (transaction is CosmosOutboxTransaction cosmosOutboxTransaction)
            {
                cosmosOutboxTransaction.StorageSession.CurrentContextBag = context;
                StorageSession = cosmosOutboxTransaction.StorageSession;
                commitOnComplete = false;
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken()) =>
            new ValueTask<bool>(false);

        public Task Open(ContextBag contextBag, CancellationToken cancellationToken = new CancellationToken())
        {
            StorageSession = new StorageSession(containerHolderResolver, contextBag);
            commitOnComplete = true;

            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return commitOnComplete ? StorageSession.Commit(cancellationToken) : Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!commitOnComplete || disposed)
            {
                return;
            }

            StorageSession.Dispose();
            disposed = true;
        }
    }
}