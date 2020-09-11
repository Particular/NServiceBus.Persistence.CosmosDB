namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    abstract class Operation
    {
        protected Operation(PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context)
        {
            PartitionKey = partitionKey;
            PartitionKeyPath = partitionKeyPath;
            Serializer = serializer;
            Context = context;
        }

        //TODO: what's the purpose of the context bag here?
        public ContextBag Context { get; }
        public PartitionKey PartitionKey { get; }
        public PartitionKeyPath PartitionKeyPath { get; }
        public JsonSerializer Serializer { get; }

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