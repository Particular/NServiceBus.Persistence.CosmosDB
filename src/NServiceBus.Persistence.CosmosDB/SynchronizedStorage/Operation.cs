namespace NServiceBus.Persistence.CosmosDB;

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

abstract class Operation(PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
    : IOperation
{
    static ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)> partitionKeyPathAndSegments;

    static readonly string[] PathSeparator = ["."];

    static ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)> PartitionKeyPathAndSegments =>
        partitionKeyPathAndSegments ??= new ConcurrentDictionary<PartitionKeyPath, (string pathToMatch, string[] segments)>();

    public ContextBag Context { get; } = context;
    public PartitionKey PartitionKey { get; } = partitionKey;
    public JsonSerializer Serializer { get; } = serializer;

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
            _ => new TransactionalBatchOperationException(result)
        };
#pragma warning restore IDE0072
    }

    public abstract void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath);
    public virtual void Dispose() { }

    protected void EnrichWithPartitionKeyIfNecessary(JObject toBeEnriched, PartitionKeyPath partitionKeyPath)
    {
        JToken partitionKeyAsJArray = JArray.Parse(PartitionKey.ToString())[0];
        (string pathToMatch, string[] segments) = PartitionKeyPathAndSegments.GetOrAdd(partitionKeyPath, path =>
        {
            string toMatch = path.ToString().Replace("/", ".");
            string[] segmentsSplit = toMatch.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            return (pathToMatch: toMatch, segments: segmentsSplit);
        });

        var start = new JObject();
        JObject current = start;
        for (int i = 0; i < segments.Length; i++)
        {
            string segmentName = segments[i];

            if (i == segments.Length - 1)
            {
                current[segmentName] = partitionKeyAsJArray;
                continue;
            }

            current[segmentName] = new JObject();
            current = (JObject)current[segmentName];
        }

        // promote it if not there, what if the user has it and the key doesn't match?
        JToken matchToken = toBeEnriched.SelectToken(pathToMatch);
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