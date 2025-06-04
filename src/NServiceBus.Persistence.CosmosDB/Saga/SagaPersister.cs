namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sagas;

class SagaPersister : ISagaPersister
{
    public SagaPersister(JsonSerializer serializer, SagaPersistenceConfiguration options)
    {
        this.serializer = serializer;
        migrationModeEnabled = options.MigrationModeEnabled;
        PessimisticLockingConfiguration lockingConfiguration = options.PessimisticLockingConfiguration;
        pessimisticLockingEnabled = lockingConfiguration.PessimisticLockingEnabled;
        leaseLockTime = lockingConfiguration.LeaseLockTime;
        acquireLeaseLockRefreshMaximumDelayMilliseconds = Convert.ToInt32(lockingConfiguration.LeaseLockAcquisitionMaximumRefreshDelay.TotalMilliseconds);
        acquireLeaseLockRefreshMinimumDelayMilliseconds = Convert.ToInt32(lockingConfiguration.LeaseLockAcquisitionMinimumRefreshDelay.TotalMilliseconds);
        acquireLeaseLockTimeout = lockingConfiguration.LeaseLockAcquisitionTimeout;
    }

    public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        var sharedTransactionalBatch = (IWorkWithSharedTransactionalBatch)session;
        PartitionKey partitionKey = GetPartitionKey(context, sagaData.Id);

        sharedTransactionalBatch.AddOperation(new SagaSave(sagaData, partitionKey, serializer, context));
        return Task.CompletedTask;
    }

    public Task Update(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        var sharedTransactionalBatch = (IWorkWithSharedTransactionalBatch)session;
        PartitionKey partitionKey = GetPartitionKey(context, sagaData.Id);

        sharedTransactionalBatch.AddOperation(new SagaUpdate(sagaData, partitionKey, serializer, context));
        return Task.CompletedTask;
    }

    public async Task<TSagaData> Get<TSagaData>(Guid sagaId, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default) where TSagaData : class, IContainSagaData
    {
        var sharedTransactionalBatch = (IWorkWithSharedTransactionalBatch)session;

        // reads need to go directly
        Container container = sharedTransactionalBatch.Container;
        PartitionKey partitionKey = GetPartitionKey(context, sagaId);

        bool sagaNotFound;
        TSagaData sagaData;
        if (!pessimisticLockingEnabled)
        {
            (sagaNotFound, sagaData) = await ReadSagaData<TSagaData>(sagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
            // if the previous lookup by id wasn't successful and the migration mode is enabled try to query for the saga data because the saga id probably represents
            // the saga id of the migrated saga.
            if (sagaNotFound && migrationModeEnabled &&
                await FindSagaIdInMigrationMode(sagaId, context, container, cancellationToken).ConfigureAwait(false) is { } migratedSagaId)
            {
                partitionKey = GetPartitionKey(context, migratedSagaId);
                (_, sagaData) = await ReadSagaData<TSagaData>(migratedSagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
            }

            return sagaData;
        }

        (sagaNotFound, sagaData) = await AcquireLease<TSagaData>(sagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
        // if the previous lookup by id wasn't successful and the migration mode is enabled try to query for the saga data because the saga id probably represents
        // the saga id of the migrated saga.
        if (sagaNotFound && migrationModeEnabled &&
            await FindSagaIdInMigrationMode(sagaId, context, container, cancellationToken).ConfigureAwait(false) is { } previousSagaId)
        {
            partitionKey = GetPartitionKey(context, previousSagaId);
            (_, sagaData) = await AcquireLease<TSagaData>(previousSagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
        }

        sharedTransactionalBatch.AddOperation(new SagaReleaseLock(sagaData, partitionKey, serializer, context));

        return sagaData;
    }

    public async Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        var sharedTransactionalBatch = (IWorkWithSharedTransactionalBatch)session;

        // Saga ID needs to be calculated the same way as in SagaIdGenerator does
        Guid sagaId = CosmosSagaIdGenerator.Generate(typeof(TSagaData), propertyName, propertyValue);

        // reads need to go directly
        Container container = sharedTransactionalBatch.Container;
        PartitionKey partitionKey = GetPartitionKey(context, sagaId);

        TSagaData sagaData;
        if (!pessimisticLockingEnabled)
        {
            (_, sagaData) = await ReadSagaData<TSagaData>(sagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
            return sagaData;
        }

        (_, sagaData) = await AcquireLease<TSagaData>(sagaId, context, container, partitionKey, cancellationToken).ConfigureAwait(false);
        sharedTransactionalBatch.AddOperation(new SagaReleaseLock(sagaData, partitionKey, serializer, context));
        return sagaData;
    }

    async Task<(bool sagaNotFound, TSagaData sagaData)> ReadSagaData<TSagaData>(Guid sagaId, ContextBag context, Container container, PartitionKey partitionKey, CancellationToken cancellationToken)
        where TSagaData : class, IContainSagaData
    {
        using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            Stream sagaStream = responseMessage.Content;

            bool sagaNotFound = responseMessage.StatusCode == HttpStatusCode.NotFound;

            return (sagaNotFound, sagaNotFound ? default : ReadSagaFromStream<TSagaData>(context, sagaStream, responseMessage));
        }
    }

    async Task<Guid?> FindSagaIdInMigrationMode(Guid sagaId, ContextBag context, Container container, CancellationToken cancellationToken)
    {
        string query =
            $@"SELECT TOP 1 c.id FROM c WHERE c[""{MetadataExtensions.MetadataKey}""][""{MetadataExtensions.SagaDataContainerMigratedSagaIdMetadataKey}""] = '{sagaId}'";
        var queryDefinition = new QueryDefinition(query);
        FeedIterator queryStreamIterator = container.GetItemQueryStreamIterator(queryDefinition);

        using (ResponseMessage iteratorResponse = await queryStreamIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false))
        {
            iteratorResponse.EnsureSuccessStatusCode();

            using (var streamReader = new StreamReader(iteratorResponse.Content))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                JObject iteratorResult = await JObject.LoadAsync(jsonReader, cancellationToken).ConfigureAwait(false);

                if (iteratorResult["Documents"] is not JArray { HasValues: true } documents)
                {
                    return default;
                }

                if (documents[0].Value<string>("id") is { } migratedSagaId)
                {
                    context.Set($"cosmos_migratedsagaid:{migratedSagaId}", sagaId);
                    return Guid.Parse(migratedSagaId);
                }

                return null;
            }
        }
    }

    async Task<(bool sagaNotFound, TSagaData sagaData)> AcquireLease<TSagaData>(Guid sagaId, ContextBag context, Container container, PartitionKey partitionKey, CancellationToken cancellationToken)
        where TSagaData : class, IContainSagaData
    {
        using (var timedTokenSource = new CancellationTokenSource(acquireLeaseLockTimeout))
        using (var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timedTokenSource.Token, cancellationToken))
        {
            CancellationToken token = combinedTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                // File time is using 100s nanoseconds which is a bit too granular for us but it is the simplest way to get
                // a deterministic increasing time value
                long now = DateTime.UtcNow.ToFileTimeUtc();
                long reservedUntil = DateTime.UtcNow.Add(leaseLockTime).ToFileTimeUtc();

                IReadOnlyList<PatchOperation> patchOperations =
                [
                    PatchOperation.Add($"/{MetadataExtensions.MetadataKey}/{MetadataExtensions.SagaDataContainerReservedUntilMetadataKey}", reservedUntil)
                ];
                var requestOptions = new PatchItemRequestOptions
                {
                    FilterPredicate =
                        $"from c where (NOT IS_DEFINED(c[\"{MetadataExtensions.MetadataKey}\"][\"{MetadataExtensions.SagaDataContainerReservedUntilMetadataKey}\"]) OR c[\"{MetadataExtensions.MetadataKey}\"][\"{MetadataExtensions.SagaDataContainerReservedUntilMetadataKey}\"] < {now})"
                };

                using (ResponseMessage responseMessage = await container.PatchItemStreamAsync(sagaId.ToString(), partitionKey,
                           patchOperations, requestOptions, token).ConfigureAwait(false))
                {
                    Stream sagaStream = responseMessage.Content;

                    bool throttlingRequired = false;
                    int refreshMinimumDelayMilliseconds = acquireLeaseLockRefreshMinimumDelayMilliseconds;
                    int refreshMaximumDelayMilliseconds = acquireLeaseLockRefreshMaximumDelayMilliseconds;

                    if (responseMessage.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        throttlingRequired = true;
                    }

                    // In case of TooManyRequests we might be violating the AcquireLeaseLockRefreshMaximumDelayMilliseconds but that's OK
                    if ((int)responseMessage.StatusCode == 429 && responseMessage.Headers.TryGetValue("x-ms-retry-after-ms",
                            out string operationWaitTime)) // TooManyRequests
                    {
                        throttlingRequired = true;

                        int retryOperationWaitTimeInMilliseconds = Convert.ToInt32(operationWaitTime);
                        refreshMinimumDelayMilliseconds = Math.Max(retryOperationWaitTimeInMilliseconds, refreshMinimumDelayMilliseconds);
                        refreshMaximumDelayMilliseconds = Math.Max(retryOperationWaitTimeInMilliseconds, refreshMaximumDelayMilliseconds);
                    }

                    if (throttlingRequired)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(random.Next(refreshMinimumDelayMilliseconds, refreshMaximumDelayMilliseconds)), token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex.IsCausedBy(token))
                        {
                            // intentionally swallowed because we want to avoid cancellation masking the fact that we were not able to acquire the lease lock in time
                        }

                        continue;
                    }

                    bool sagaNotFound = responseMessage.StatusCode == HttpStatusCode.NotFound;

                    return (sagaNotFound, sagaNotFound ? default : ReadSagaFromStream<TSagaData>(context, sagaStream, responseMessage));
                }
            }

            throw new TimeoutException(
                $"Unable to acquire exclusive write lock for saga with id '{sagaId}' within allocated time '{leaseLockTime}'.");
        }
    }

    TSagaData ReadSagaFromStream<TSagaData>(ContextBag context, Stream sagaStream, ResponseMessage responseMessage) where TSagaData : class, IContainSagaData
    {
        using (sagaStream)
        using (var streamReader = new StreamReader(sagaStream))
        using (var jsonReader = new JsonTextReader(streamReader))
        {
            TSagaData sagaData = serializer.Deserialize<TSagaData>(jsonReader);

            // we always require the etag even when using the pessimistic locking approach in order to have retries
            // in rare edge cases like when another concurrent update has stolen the reservation
            context.Set($"cosmos_etag:{sagaData.Id}", responseMessage.Headers.ETag);

            return sagaData;
        }
    }

    public Task Complete(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        var sharedTransactionalBatch = (IWorkWithSharedTransactionalBatch)session;
        PartitionKey partitionKey = GetPartitionKey(context, sagaData.Id);

        sharedTransactionalBatch.AddOperation(new SagaDelete(sagaData, partitionKey, context));

        return Task.CompletedTask;
    }

    static PartitionKey GetPartitionKey(ContextBag context, Guid sagaDataId)
    {
        if (!context.TryGet<PartitionKey>(out PartitionKey partitionKey))
        {
            partitionKey = new PartitionKey(sagaDataId.ToString());
        }

        return partitionKey;
    }

    JsonSerializer serializer;
    readonly bool migrationModeEnabled;
    readonly bool pessimisticLockingEnabled;
    readonly TimeSpan leaseLockTime;
    readonly int acquireLeaseLockRefreshMaximumDelayMilliseconds;
    readonly int acquireLeaseLockRefreshMinimumDelayMilliseconds;
    readonly TimeSpan acquireLeaseLockTimeout;
    static readonly Random random = new();
}
