namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NServiceBus.Extensibility;
    using NServiceBus.Outbox;
    using NServiceBus.Pipeline;

    class OutboxPersister : IOutboxStorage
    {
        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            return Task.FromResult((OutboxTransaction)new CosmosOutboxTransaction());
        }

        public Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            //This always must return null for the Outbox "hack" to work
            return null;
        }

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
            var cosmosTransaction = transaction as CosmosOutboxTransaction;

            if (cosmosTransaction.TransactionalBatch is null)
            {
                return Task.CompletedTask;
            }

            //TODO: Probably serialize and add keypath/partitionkey JTokens and id

            cosmosTransaction.TransactionalBatch.CreateItem(new OutboxRecord { Id = message.MessageId, TransportOperations = message.TransportOperations });

            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context)
        {
            var partitionKey = context.Get<string>(ContextBagKeys.PartitionKeyValue);
            var partitionKeyPath = context.Get<string>(ContextBagKeys.PartitionKeyPath);
            var container = context.Get<Container>();
            var logicalMessageId = context.Get<string>(ContextBagKeys.LogicalMessageId);

            //TODO: we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = partitionKeyPath.Replace("/", ".");
            var segments = pathToMatch.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if (i == segments.Length - 1)
                {
                    current[segmentName] = partitionKey;
                    continue;
                }

                current[segmentName] = new JObject();
                current = current[segmentName] as JObject;
            }

            var outboxRecord = new OutboxRecord
            {
                Id = logicalMessageId,
                Dispatched = true
            };

            var createJObject = JObject.FromObject(outboxRecord);

            createJObject.Add("ttl", 100);

            createJObject.Merge(start);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(createJObject))))
            {
                var result = await container.UpsertItemStreamAsync(stream, new PartitionKey(partitionKey)).ConfigureAwait(false);

                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new Exception($"Unable to update the outbox record: {result.ErrorMessage}");
                }
            }
        }
    }
}
