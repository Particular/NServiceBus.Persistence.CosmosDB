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
        public StorageSession(Container container, PartitionKey partitionKey, string partitionKeyPath)
        {
            this.partitionKeyPath = partitionKeyPath;
            Container = container;
            PartitionKey = partitionKey;
        }

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

        public async Task CompleteAsync()
        {
            var partitionKey = JArray.Parse(PartitionKey.ToString())[0];

            // we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = partitionKeyPath.Replace("/", ".");
            var segments = pathToMatch.Split(new[]{ "." }, StringSplitOptions.RemoveEmptyEntries);

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if(i == segments.Length -1)
                {
                    current[segmentName] = partitionKey;
                    continue;
                }

                current[segmentName] = new JObject();
                current = current[segmentName] as JObject;
            }


            var mappingDictionary = new Dictionary<int, SagaModification>();
            foreach (var modification in Modifications)
            {
                switch (modification)
                {
                    case SagaSave sagaSave:
                        var createJObject = JObject.FromObject(sagaSave.SagaData);

                        createJObject.Add("id", sagaSave.SagaData.Id.ToString());
                        var saveMetadata = new JObject
                        {
                            { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
                        };
                        createJObject.Add(MetadataExtensions.MetadataKey,saveMetadata);

                        // promote it if not there, what if the user has it and the key doesn't match?
                        var createdMatchToken = createJObject.SelectToken(pathToMatch);
                        if (createdMatchToken == null)
                        {
                            createJObject.Merge(start);
                        }

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
                        var UpdateMetadata = new JObject
                        {
                            { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
                        };
                        updateJObject.Add(MetadataExtensions.MetadataKey,UpdateMetadata);

                        // promote it if not there, what if the user has it and the key doesn't match?
                        var updatedMatchToken = updateJObject.SelectToken(pathToMatch);
                        if (updatedMatchToken == null)
                        {
                            updateJObject.Merge(start);
                        }

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
                        var deleteOptions = new TransactionalBatchItemRequestOptions {IfMatchEtag = deleteEtag};
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

                        if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            switch (modification)
                            {
                                case SagaDelete sagaDelete:
                                    throw new Exception($"The '{sagaDelete.SagaData.GetType().Name}' saga with id '{sagaDelete.SagaData.Id}' can't be completed because it was updated by another process.");
                                case SagaSave sagaSave:
                                    throw new Exception($"The '{sagaSave.SagaData.GetType().Name}' saga with id '{sagaSave.SagaData.Id}' could not be created possibly due to a concurrency conflict.");
                                case SagaUpdate sagaUpdate:
                                    throw new Exception($"The '{sagaUpdate.SagaData.GetType().Name}' saga with id '{sagaUpdate.SagaData.Id}' was updated by another process or no longer exists.");
                                default:
                                    throw new Exception("Concurrency conflict.");
                            }
                        }
                    }

                    if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        throw new Exception("Concurrency conflict.");
                    }

                    if(result.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new Exception("Bad request. Quite likely the partition key did not match");
                    }
                }
            }
        }

        public void Dispose()
        {
            transactionalBatchDecorator?.Dispose();
        }

        TransactionalBatchDecorator transactionalBatchDecorator;
        string partitionKeyPath;
    }
}