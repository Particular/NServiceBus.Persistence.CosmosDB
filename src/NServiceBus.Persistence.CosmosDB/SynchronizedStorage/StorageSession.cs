namespace NServiceBus.Persistence.CosmosDB
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

    class StorageSession : CompletableSynchronizedStorageSession
    {
        TransactionalBatchDecorator transactionalBatchDecorator;
        public Container Container { get; }

        public TransactionalBatchDecorator TransactionalBatch
        {
            get
            {
                if (transactionalBatchDecorator == null)
                {
                    transactionalBatchDecorator = new TransactionalBatchDecorator(Container.CreateTransactionalBatch(PartitionKey));
                }

                return transactionalBatchDecorator;
            }
        }
        public PartitionKey PartitionKey { get; }

        public List<SagaModification> Modifications { get; } = new List<SagaModification>();

        public StorageSession(Container container, PartitionKey partitionKey)
        {
            Container = container;
            PartitionKey = partitionKey;
        }

        public async Task CompleteAsync()
        {
            var partitionKey = JArray.Parse(PartitionKey.ToString());
            var mappingDictionary = new Dictionary<int, SagaModification>();
            foreach (var modification in Modifications)
            {
                switch (modification)
                {
                    case SagaSave sagaSave:
                        var createJObject = JObject.FromObject(sagaSave.SagaData);

                        createJObject.Add("id", sagaSave.SagaData.Id.ToString());
                        createJObject.Add("partitionKey", partitionKey[0]);
                        // var metaData = new JObject
                        // {
                        //     { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
                        // };
                        // jObject.Add(MetadataExtensions.MetadataKey,metaData);

                        // has to be kept open
                        var createStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(createJObject)));
                        var createOptions = new TransactionalBatchItemRequestOptions
                        {
                            EnableContentResponseOnWrite = false
                        };
                        TransactionalBatch.CreateItemStream(createStream, createOptions);
                        mappingDictionary[TransactionalBatch.Index] = sagaSave;
                        break;
                    case SagaUpdate sagaUpdate:
                        var updateJObject = JObject.FromObject(sagaUpdate.SagaData);

                        updateJObject.Add("id", sagaUpdate.SagaData.Id.ToString());
                        updateJObject.Add("partitionKey", partitionKey[0]);
                        // var metaData = new JObject
                        // {
                        //     { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
                        // };
                        // jObject.Add(MetadataExtensions.MetadataKey,metaData);

                        // only update if we have the same version as in CosmosDB
                        sagaUpdate.Context.TryGet<string>($"cosmos_etag:{sagaUpdate.SagaData.Id}", out var updateEtag);
                        var updateOptions = new TransactionalBatchItemRequestOptions
                        {
                            IfMatchEtag = updateEtag,
                            EnableContentResponseOnWrite = false,
                        };

                        // has to be kept open
                        var updateStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(updateJObject)));
                        TransactionalBatch.ReplaceItemStream(sagaUpdate.SagaData.Id.ToString(), updateStream, updateOptions);
                        mappingDictionary[TransactionalBatch.Index] = sagaUpdate;
                        break;
                    case SagaDelete sagaDelete:

                        // only delete if we have the same version as in CosmosDB
                        sagaDelete.Context.TryGet<string>($"cosmos_etag:{sagaDelete.SagaData.Id}", out var deleteEtag);
                        var deleteOptions = new TransactionalBatchItemRequestOptions { IfMatchEtag = deleteEtag };
                        TransactionalBatch.DeleteItem(sagaDelete.SagaData.Id.ToString(), deleteOptions);
                        mappingDictionary[TransactionalBatch.Index] = sagaDelete;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(modification));
                }
            }

            if (!TransactionalBatch.CanBeExecuted)
            {
                return;
            }

            using (var batchOutcomeResponse = await TransactionalBatch.Inner.ExecuteAsync().ConfigureAwait(false))
            {
                for (var i = 0; i < batchOutcomeResponse.Count; i++)
                {
                    var result = batchOutcomeResponse[i];

                    if (mappingDictionary.TryGetValue(i, out var modification))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            modification.Context.Set($"cosmos_etag:{modification.SagaData.Id}", result.ETag);
                            continue;
                        }

                        // TODO: Provide more context in case of failure
                    }

                    if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        // technically would could somehow map back to what we wrote if we store extra info in the session
                        throw new Exception("Concurrent updates lead to write conflicts.");
                    }
                }
            }
        }

        public void Dispose()
        {
            transactionalBatchDecorator?.Dispose();
        }
    }
}