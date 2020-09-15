namespace NServiceBus.Persistence.CosmosDB
{
    using System.IO;
    using Microsoft.Azure.Cosmos;

    abstract class UserOperation : Operation
    {
        protected readonly TransactionalBatchItemRequestOptions options;

        public UserOperation(TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(partitionKey, new PartitionKeyPath(""), null, null)
        {
            this.options = options;
        }
    }

    abstract class StreamUserOperation : UserOperation
    {
        protected readonly Stream streamPayload;

        public StreamUserOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.streamPayload = streamPayload;
        }

        public override void Dispose() => streamPayload.Dispose();
    }

    sealed class CreateItemOperation<T> : UserOperation
    {
        readonly T item;

        public CreateItemOperation(T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.item = item;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.CreateItem<T>(item, options);
        }
    }

    sealed class CreateItemStreamOperation : StreamUserOperation
    {
        public CreateItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.CreateItemStream(streamPayload, options);
        }
    }

    sealed class ReadItemOperation : UserOperation
    {
        readonly string id;

        public ReadItemOperation(string id, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.id = id;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.ReadItem(id, options);
        }
    }

    sealed class UpsertItemOperation<T> : UserOperation
    {
        readonly T item;

        public UpsertItemOperation(T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.item = item;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.UpsertItem<T>(item, options);
        }
    }

    sealed class UpsertItemStreamOperation : StreamUserOperation
    {
        public UpsertItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.UpsertItemStream(streamPayload, options);
        }
    }

    sealed class ReplaceItemOperation<T> : UserOperation
    {
        readonly T item;
        readonly string id;

        public ReplaceItemOperation(string id, T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.id = id;
            this.item = item;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.ReplaceItem<T>(id, item, options);
        }
    }

    sealed class ReplaceItemStreamOperation : StreamUserOperation
    {
        readonly string id;

        public ReplaceItemStreamOperation(string id, Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
            this.id = id;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.ReplaceItemStream(id, streamPayload, options);
        }
    }

    sealed class DeleteItemOperation : UserOperation
    {
        readonly string id;

        public DeleteItemOperation(string id, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.id = id;
        }

        public override void Apply(TransactionalBatch transactionalBatch)
        {
            transactionalBatch.DeleteItem(id, options);
        }
    }
}
