namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Newtonsoft.Json.Linq;

    static class JObjectExtensions
    {
        public static void EnrichWithPartitionKeyIfNecessary(this JObject toBeEnriched, string partitionKey, string partitionKeyPath)
        {
            var partitionKeyAsJArray = JArray.Parse(partitionKey)[0];
            // we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = partitionKeyPath.Replace("/", ".");
            var segments = pathToMatch.Split(new[] {"."}, StringSplitOptions.RemoveEmptyEntries);

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
}