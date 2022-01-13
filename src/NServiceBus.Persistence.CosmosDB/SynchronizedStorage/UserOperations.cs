namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos;

    abstract class UserOperation : Operation
    {
        protected readonly TransactionalBatchItemRequestOptions options;

        protected UserOperation(TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(partitionKey, null, null)
        {
            this.options = options;
        }
    }

    abstract class StreamUserOperation : UserOperation
    {
        protected readonly Stream streamPayload;

        protected StreamUserOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
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

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            transactionalBatch.CreateItem(item, options);
        }
    }

    sealed class CreateItemStreamOperation : StreamUserOperation
    {
        public CreateItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
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

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
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

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            transactionalBatch.UpsertItem(item, options);
        }
    }

    sealed class UpsertItemStreamOperation : StreamUserOperation
    {
        public UpsertItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
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

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            transactionalBatch.ReplaceItem(id, item, options);
        }
    }

    sealed class ReplaceItemStreamOperation : StreamUserOperation
    {
        readonly string id;

        public ReplaceItemStreamOperation(string id, Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey) : base(streamPayload, options, partitionKey)
        {
            this.id = id;
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
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

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            transactionalBatch.DeleteItem(id, options);
        }
    }

    sealed class PatchItemOperation : UserOperation
    {
        readonly string id;
        readonly IReadOnlyList<PatchOperation> patchOperations;

        public PatchItemOperation(string id, IReadOnlyList<PatchOperation> patchOperations, TransactionalBatchPatchItemRequestOptions options, PartitionKey partitionKey) : base(options, partitionKey)
        {
            this.id = id;
            this.patchOperations = patchOperations;
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            transactionalBatch.PatchItem(id, patchOperations, (TransactionalBatchPatchItemRequestOptions)options);
        }
    }
}
