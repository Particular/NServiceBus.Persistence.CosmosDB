namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class Exporter
    {
        static readonly Regex secondaryIndexRegex = new Regex("Index_(?<SagaDataType>.*)_(?<PropertyName>.*)_\"(?<PropertyValue>.*)\"#", RegexOptions.Compiled);
        static readonly Regex probablyJArrayRegex = new Regex("\\[.*\\]", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex probablyJObjectRegex = new Regex("\\{.*\\}", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex probablyJokenValueRegex = new Regex("\".*\"", RegexOptions.Singleline | RegexOptions.Compiled);

        public static async Task Run(ILogger logger, string connectionString, string tableName, string workingPath, CancellationToken cancellationToken)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudTableClient();

            var tablePath = Path.Combine(workingPath, tableName);

            if (!Directory.Exists(tablePath))
            {
                Directory.CreateDirectory(tablePath);
            }
            else
            {
                logger.LogWarning($"The sub-directory '{tableName}' already exists and might contain unrelated files.");
            }

            var table = client.GetTableReference(tableName);

            var query = new TableQuery<DictionaryTableEntity>();

            await foreach (var fileWritten in StreamToFiles(logger, table, query, tableName, workingPath, cancellationToken))
            {
                logger.Log(LogLevel.Information, $"Writing of '{fileWritten}' done.");
            }
        }

        static async IAsyncEnumerable<string> StreamToFiles(ILogger logger,
            CloudTable table,
            TableQuery<DictionaryTableEntity> query,
            string tableName,
            string workingPath,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var throttler = new SemaphoreSlim(50);
            var tasks = new List<Task<string>>();
            await foreach (var entity in table.ExecuteQueryAsync(query, ct: cancellationToken))
            {
                if (entity.PartitionKey.StartsWith("Index_"))
                {
                    logger.Log(LogLevel.Debug, $"Skipped row '{entity.PartitionKey}'");
                    continue;
                }

                tasks.Add(WriteEntityToFile(entity, tableName, throttler, workingPath, cancellationToken));
            }

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(done);

                yield return await done
                    .ConfigureAwait(false);
            }
        }

        static async Task<string> WriteEntityToFile(DictionaryTableEntity entity, string tableName, SemaphoreSlim throttler, string workingPath, CancellationToken cancellationToken)
        {
            try
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);

                var (jObject, newSagaId) = Convert(entity);

                var filePath = Path.Combine(workingPath, tableName, $"{newSagaId}.json");
                await using var fileWriter = File.CreateText(filePath);
                using var jsonTextWriter = new JsonTextWriter(fileWriter)
                {
                    Formatting = Formatting.Indented
                };
                await jObject.WriteToAsync(jsonTextWriter, cancellationToken).ConfigureAwait(false);
                await jsonTextWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                await fileWriter.FlushAsync().ConfigureAwait(false);
                return filePath;
            }
            finally
            {
                throttler.Release();
            }
        }

        static (JObject converted, Guid newSagaId) Convert(DictionaryTableEntity entity)
        {
            // The rows we're processing would have the following column name and example value structure:
            // Column name:   NServiceBus_2ndIndexKey
            // Value example: Index_Samples.OrderSagaData_OrderId_"a3413eda-fb98-46c1-a44e-89da9efada16"#

            var match = secondaryIndexRegex.Match(entity["NServiceBus_2ndIndexKey"].StringValue);
            var sagaDataTypeFullName = match.Groups["SagaDataType"].Value;
            var propertyName = match.Groups["PropertyName"].Value;
            var propertyValue = match.Groups["PropertyValue"].Value;

            var newSagaId = SagaIdGenerator.Generate(sagaDataTypeFullName, propertyName, propertyValue);
            var oldSagaId = entity["Id"].GuidValue;

            entity.Remove("NServiceBus_2ndIndexKey");
            entity.Remove("PartitionKey");
            entity.Remove("RowKey");
            entity.Remove("Id");

            var jObject = new JObject();

            var metadata = new JObject
            {
                {NServiceBus.Persistence.CosmosDB.MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, NServiceBus.Persistence.CosmosDB.SagaSchemaVersion.Current},
                {NServiceBus.Persistence.CosmosDB.MetadataExtensions.SagaDataContainerFullTypeNameMetadataKey, sagaDataTypeFullName},
                {NServiceBus.Persistence.CosmosDB.MetadataExtensions.SagaDataContainerMigratedSagaIdMetadataKey, oldSagaId.ToString()}
            };
            jObject.Add(NServiceBus.Persistence.CosmosDB.MetadataExtensions.MetadataKey, metadata);

            jObject.Add("id", newSagaId);

            foreach (var (key, value) in entity)
            {
                switch (value.PropertyType)
                {
                    case EdmType.String when probablyJArrayRegex.IsMatch(value.StringValue):
                        try
                        {
                            var propertyAsJArray = JArray.Parse(value.StringValue);
                            jObject.Add(key, propertyAsJArray);
                        }
                        catch (JsonReaderException)
                        {
                            jObject.Add(key, value.StringValue);
                        }
                        break;
                    case EdmType.String when probablyJObjectRegex.IsMatch(value.StringValue):
                        try
                        {
                            var propertyAsJObject = JObject.Parse(value.StringValue);
                            jObject.Add(key, propertyAsJObject);
                        }
                        catch (JsonReaderException)
                        {
                            jObject.Add(key, value.StringValue);
                        }
                        break;
                    case EdmType.String when probablyJokenValueRegex.IsMatch(value.StringValue):
                        try
                        {
                            var propertyAsJToken = JToken.Parse(value.StringValue);
                            jObject.Add(key, propertyAsJToken);
                        }
                        catch (JsonReaderException)
                        {
                            jObject.Add(key, value.StringValue);
                        }
                        break;
                    case EdmType.Binary:
                        jObject.Add(key, value.BinaryValue);
                        break;
                    case EdmType.Boolean:
                        jObject.Add(key, value.BooleanValue);
                        break;
                    case EdmType.DateTime:
                        jObject.Add(key, value.DateTime);
                        break;
                    case EdmType.Double:
                        jObject.Add(key, value.DoubleValue);
                        break;
                    case EdmType.Guid:
                        jObject.Add(key, value.GuidValue);
                        break;
                    case EdmType.Int32:
                        jObject.Add(key, value.Int32Value);
                        break;
                    case EdmType.Int64:
                        jObject.Add(key, value.Int64Value);
                        break;
                    default:
                        jObject.Add(key, value.StringValue);
                        break;
                }
            }

            return (jObject, newSagaId);
        }
    }
}