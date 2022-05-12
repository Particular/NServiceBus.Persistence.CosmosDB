namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    interface IReleaseLockOperation : IOperation
    {
    }

    interface IOperation : IDisposable
    {
        PartitionKey PartitionKey { get; }
        void Success(TransactionalBatchOperationResult result);
        void Conflict(TransactionalBatchOperationResult result);
        void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath);
    }

    abstract class Operation : IOperation
    {
        static ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)> partitionKeyPathAndSegments;

        static readonly string[] PathSeparator = { "." };

        static ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)> PartitionKeyPathAndSegments =>
            partitionKeyPathAndSegments ??= new ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)>();

        protected Operation(PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
        {
            PartitionKey = partitionKey;
            Serializer = serializer;
            Context = context;
        }

        public ContextBag Context { get; }
        public PartitionKey PartitionKey { get; }
        public JsonSerializer Serializer { get; }

        public virtual void Success(TransactionalBatchOperationResult result)
        {
        }

        public virtual void Conflict(TransactionalBatchOperationResult result)
        {
            if ((int)result.StatusCode == 424) // HttpStatusCode.FailedDependency:
            {
                return;
            }

#pragma warning disable IDE0072 // We have the default case and don't need to list 300 HTTP codes
            throw result.StatusCode switch
            {
                HttpStatusCode.BadRequest => new TransactionalBatchOperationException("Bad request. Likely the partition key did not match.", result),
                HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed => new TransactionalBatchOperationException("Concurrency conflict.", result),
                _ => new TransactionalBatchOperationException(result),
            };
#pragma warning restore IDE0072
        }
        public abstract void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath);
        public virtual void Dispose() { }

        protected void EnrichWithPartitionKeyIfNecessary(JObject toBeEnriched, PartitionKeyPath partitionKeyPath)
        {
            var partitionKeyAsJArray = JArray.Parse(PartitionKey.ToString())[0];
            var (pathToMatch, segments) = PartitionKeyPathAndSegments.GetOrAdd(partitionKeyPath, path =>
            {
                var toMatch = path.ToString().Replace("/", ".");
                var segmentsSplit = toMatch.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                return (pathToMatch: toMatch, segments: segmentsSplit);
            });

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if (i == segments.Length - 1)
                {
                    current[segmentName] = partitionKeyAsJArray;
                    continue;
                }

                current[segmentName] = new JObject();
                current = (JObject)current[segmentName];
            }

            // promote it if not there, what if the user has it and the key doesn't match?
            var matchToken = toBeEnriched.SelectToken(pathToMatch);
            if (matchToken == null)
            {
                toBeEnriched.Merge(start);
            }
        }
    }

    class ThrowOnConflictOperation : Operation
    {
        ThrowOnConflictOperation() : base(PartitionKey.Null, null, null)
        {
        }

        public static IOperation Instance { get; } = new ThrowOnConflictOperation();

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
        }
    }
}