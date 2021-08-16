﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Outbox;

    class CosmosOutboxTransaction : IOutboxTransaction
    {
        public StorageSession StorageSession { get; }
        public PartitionKey? PartitionKey { get; set; }

        // By default, store and commit are enabled
        public bool SuppressStoreAndCommit { get; set; }

        public CosmosOutboxTransaction(ContainerHolderResolver resolver, ContextBag context)
        {
            StorageSession = new StorageSession(resolver, context, false);
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            if (SuppressStoreAndCommit)
            {
                return Task.CompletedTask;
            }

            return StorageSession.Commit(cancellationToken);
        }

        public void Dispose()
        {
            StorageSession.Dispose();
        }
    }
}