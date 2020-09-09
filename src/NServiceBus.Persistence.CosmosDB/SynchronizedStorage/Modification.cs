namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    abstract class Modification
    {
        protected Modification(ContextBag context)
        {
            Context = context;
        }

        public ContextBag Context { get; }

        public virtual void Success(TransactionalBatchOperationResult result)
        {
        }

        public virtual void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception("Concurrency conflict.");
        }

        public abstract void Apply(TransactionalBatchDecorator transactionalBatch, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath);

        protected void EnrichWithPartitionKeyIfNecessary(JObject toBeEnriched, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            var partitionKeyAsJArray = JArray.Parse(partitionKey.ToString())[0];
            // we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = ((string)partitionKeyPath).Replace("/", ".");
            var segments = pathToMatch.Split(new[]{ "." }, StringSplitOptions.RemoveEmptyEntries);

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if(i == segments.Length -1)
                {
                    current[segmentName] = partitionKeyAsJArray;
                    continue;
                }

                current[segmentName] = new JObject();
                current = (JObject)current[segmentName];
            }

            // promote it if not there, what if the user has it and the key doesn't match?
            var createdMatchToken = toBeEnriched.SelectToken(pathToMatch);
            if (createdMatchToken == null)
            {
                toBeEnriched.Merge(start);
            }
        }
    }
}