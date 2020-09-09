namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

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
            var matchToken = toBeEnriched.SelectToken(pathToMatch);
            if (matchToken == null)
            {
                toBeEnriched.Merge(start);
            }
        }
    }
}