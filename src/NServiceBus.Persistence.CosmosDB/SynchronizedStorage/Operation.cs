namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;

    abstract class Operation
    {
        protected Operation(ContextBag context, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            Context = context;
            PartitionKey = partitionKey;
            PartitionKeyPath = partitionKeyPath;
        }

        //TODO: what's the purpose of the context bag here?
        public ContextBag Context { get; }
        public PartitionKey PartitionKey { get; }
        public PartitionKeyPath PartitionKeyPath { get; }

        public virtual void Success(TransactionalBatchOperationResult result)
        {
        }

        public virtual void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception("Concurrency conflict.");
        }

        public abstract void Apply(TransactionalBatchDecorator transactionalBatch);
    }
}