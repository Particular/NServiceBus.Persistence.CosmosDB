namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    static class Exporter
    {
        static readonly Regex secondaryIndexRegex = new Regex("Index_(?:.*)_(?<PropertyName>.*)_\"(?<PropertyValue>.*)\"#", RegexOptions.Compiled);
        static readonly Regex probablyJArrayRegex = new Regex("\\[.*\\]", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex probablyJObjectRegex = new Regex("\\{.*\\}", RegexOptions.Multiline | RegexOptions.Compiled);

        public static async Task Run(ILogger logger, string connectionString, string sagaTypeFullName, CancellationToken cancellationToken)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudTableClient();

            var tableName = sagaTypeFullName.Split('.').Last();

            if (!Directory.Exists(tableName))
            {
                Directory.CreateDirectory(tableName);
            }

            var table = client.GetTableReference(tableName);

            var query = new TableQuery<DictionaryTableEntity>();

            await foreach (var fileWritten in StreamToFiles(logger, table, query, sagaTypeFullName, tableName, cancellationToken))
            {
                logger.Log(LogLevel.Information, $"Writing of '{fileWritten}' done.");
            }
        }

        static async IAsyncEnumerable<string> StreamToFiles(ILogger logger,
            CloudTable table,
            TableQuery<DictionaryTableEntity> query,
            string sagaTypeFullName,
            string tableName,
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

                tasks.Add(WriteEntityToFile(sagaTypeFullName, entity, tableName, throttler, cancellationToken));
            }

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(done);

                yield return await done
                    .ConfigureAwait(false);
            }
        }

        static async Task<string> WriteEntityToFile(string sagaTypeFullName, DictionaryTableEntity entity, string tableName, SemaphoreSlim throttler, CancellationToken cancellationToken)
        {
            try
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);

                var (jObject, newSagaId) = Convert(entity, sagaTypeFullName);

                var filePath = Path.Combine(tableName, $"{newSagaId}.json");
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

        static (JObject converted, Guid newSagaId) Convert(DictionaryTableEntity entity, string sagaTypeFullName)
        {
            // NServiceBus_2ndIndexKey
            // Index_OrderSagaData_OrderId_"a3413eda-fb98-46c1-a44e-89da9efada16"#

            var match = secondaryIndexRegex.Match(entity["NServiceBus_2ndIndexKey"].StringValue);
            var propertyName = match.Groups["PropertyName"].Value;
            var propertyValue = match.Groups["PropertyValue"].Value;

            entity.Remove("NServiceBus_2ndIndexKey");
            entity.Remove("PartitionKey");
            entity.Remove("RowKey");

            var newSagaId = SagaIdGenerator.Generate(sagaTypeFullName, propertyName, propertyValue);
            var oldSagaId = entity["Id"].GuidValue;

            var jObject = new JObject();

            // maybe somehow use the constants from the persister, currently hardcoded to move forward
            var metadata = new JObject
            {
                {"SagaDataContainer-SchemaVersion", "1.0.0"},
                {"SagaDataContainer-FullTypeName", sagaTypeFullName}, // just some random proposals to move forward
                {"SagaDataContainer-OldSagaId", oldSagaId.ToString()} // just some random proposals to move forward
            };
            jObject.Add("_NServiceBus-Persistence-Metadata", metadata);

            jObject.Add("_id", newSagaId);

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
                            // TODO: What to do here?
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
                            // TODO: What to do here?
                        }

                        break;
                    case EdmType.Binary:
                        jObject.Add(key, value.BinaryValue);
                        break;
                    case EdmType.Boolean:
                        jObject.Add(key, value.BooleanValue);
                        break;
                    case EdmType.DateTime:
                        if (value.DateTimeOffsetValue.HasValue)
                        {
                            jObject.Add(key, value.DateTimeOffsetValue.Value);
                        }
                        else
                        {
                            jObject.Add(key, value.DateTime);
                        }

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