namespace NServiceBus.Persistence.CosmosDB
{
    using System.Net;
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
            switch (result.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    throw new TransactionalBatchOperationException("Bad request. Likely the partition key did not match", result);
                case HttpStatusCode.Conflict:
                case HttpStatusCode.PreconditionFailed:
                    throw new TransactionalBatchOperationException("Concurrency conflict.", result);
                default:
                    throw new TransactionalBatchOperationException(result);
            }
        }

        public abstract void Apply(TransactionalBatchDecorator transactionalBatch);
    }

    class ThrowOnConflictOperation : Operation
    {
        private ThrowOnConflictOperation() : base(PartitionKey.Null, default, null, null)
        {
        }

        public static Operation Instance { get; } = new ThrowOnConflictOperation();

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
        }
    }
}