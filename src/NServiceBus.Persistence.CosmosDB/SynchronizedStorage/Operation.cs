namespace NServiceBus.Persistence.CosmosDB
{
    using System.Net;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    abstract class Operation
    {
        protected Operation(PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer)
        {
            PartitionKey = partitionKey;
            PartitionKeyPath = partitionKeyPath;
            Serializer = serializer;
        }

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
                    throw new TransactionalBatchOperationException("Bad request. Likely the partition key did not match.", result);
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
        private ThrowOnConflictOperation() : base(PartitionKey.Null, default, null)
        {
        }

        public static Operation Instance { get; } = new ThrowOnConflictOperation();

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
        }
    }
}