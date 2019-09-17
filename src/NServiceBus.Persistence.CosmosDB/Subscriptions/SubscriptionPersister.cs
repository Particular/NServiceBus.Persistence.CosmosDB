namespace NServiceBus.Features
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Microsoft.Azure.Cosmos;
    using Persistence.CosmosDB;
    using Persistence.CosmosDB.Subscriptions;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    class SubscriptionPersister : ISubscriptionStorage
    {
        static readonly ILog Log = LogManager.GetLogger<SubscriptionPersister>();
        Container container;

        public SubscriptionPersister(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            var id = EventSubscriptionIdGenerator.Generate(subscriber.Endpoint, subscriber.TransportAddress, messageType);
            var subscription = new EventSubscriptionDocument
            {
                Id = id,
                PartitionKey = id.ToString(),
                Endpoint = subscriber.Endpoint,
                TransportAddress = subscriber.TransportAddress,
                MessageTypeName = messageType.TypeName,
            };

            if (subscriber.Endpoint != null)
            {
                ItemResponse<EventSubscriptionDocument> response = null;

                try
                {
                    response = await container.ReadItemAsync<EventSubscriptionDocument>(id.ToString(), new PartitionKey(id.ToString())).ConfigureAwait(false);
                }
                catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
                {
                }

                _ = await container.UpsertItemAsync(subscription, new PartitionKey(id.ToString()), new ItemRequestOptions { IfMatchEtag = response?.ETag }).ConfigureAwait(false);

                if (response == null)
                {
                    Log.DebugFormat("Created new subscription for '{0}' on '{1}'", subscriber.TransportAddress, messageType.TypeName);
                }
                else
                {
                    Log.DebugFormat("Updated existing subscription of '{0}' on '{1}'", subscriber.TransportAddress, messageType.TypeName);
                }
            }
            else
            {
                // support for older versions of NServiceBus which do not provide a logical endpoint name. We do not want to replace a non null value with null.
                try
                {
                    await container.CreateItemAsync(subscription).ConfigureAwait(false);
                    Log.DebugFormat("Created legacy subscription for '{0}' on '{1}'", subscriber.TransportAddress, messageType.TypeName);
                }
                catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict)
                {
                    // duplicate key error which means a document already exists
                    // existing subscriptions should not be stripped of their logical endpoint name
                    Log.DebugFormat("Skipping legacy subscription for '{0}' on '{1}' because a newer subscription already exists", subscriber.TransportAddress, messageType.TypeName);
                }
            }
        }

        public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            var query = new QueryDefinition($"SELECT * FROM c WHERE c.messageTypeName = '{messageType}' AND c.transportAddress = '{subscriber.TransportAddress}'");

            var resultSet = container.GetItemQueryIterator<EventSubscriptionDocument>(query,
                requestOptions: new QueryRequestOptions
                {
                    EnableScanInQuery = true
                });

            var documents = new List<EventSubscriptionDocument>();

            while (resultSet.HasMoreResults)
            {
                var doc = (await resultSet.ReadNextAsync().ConfigureAwait(false)).First();

                documents.Add(doc);
            }

            var tasks = new List<Task>(documents.Count);
            foreach (var foundSubscriber in documents)
            {
                tasks.Add(container.DeleteItemAsync<EventSubscriptionDocument>(foundSubscriber.Id.ToString(), new PartitionKey(foundSubscriber.PartitionKey)));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Log.DebugFormat("Deleted {0} subscriptions for address '{1}' on message type '{2}'", documents.Count , subscriber.TransportAddress, messageType.TypeName);
        }

        public async Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context)
        {
            var messageTypeNames = messageTypes.Select(t => t.TypeName).ToArray();

            var query = new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@messageTypes, c.messageTypeName, false)")
                .WithParameter("@messageTypes", "[" + string.Join(",", messageTypeNames) + "]");

            var resultSet = container.GetItemQueryIterator<EventSubscriptionDocument>(query,
                requestOptions: new QueryRequestOptions
                {
                    EnableScanInQuery = true
                });

            var foundSubscribers = new List<Subscriber>();

            while (resultSet.HasMoreResults)
            {
                var doc = (await resultSet.ReadNextAsync().ConfigureAwait(false)).First();

                foundSubscribers.Add(new Subscriber(doc.TransportAddress, doc.Endpoint));
            }

            return foundSubscribers;
        }
    }
}