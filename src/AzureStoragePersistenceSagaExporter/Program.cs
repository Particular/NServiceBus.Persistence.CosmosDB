namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Cosmos.Table;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "migrate"
            };

            var verboseOption = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, true);
            var sagaDataTypeOption = app.Option<string>($"-t|--{ApplicationOptions.SagaDataFullTypeName}", "The full type name (namespace + type) of the saga data to export", CommandOptionType.SingleValue);
            var connectionStringOption = app.Option<string>($"-c|--{ApplicationOptions.ConnectionString}", "The connection string to the Azure Storage account with the saga data.", CommandOptionType.SingleValue);

            app.HelpOption(inherited: true);
            var runParameters = new Dictionary<string, string>();

            app.OnExecuteAsync(async cancellationToken =>
            {
                var account = CloudStorageAccount.Parse(connectionStringOption.Value());
                var client = account.CreateCloudTableClient();

                var sagaTypeFullName = sagaDataTypeOption.Value();

                var tableName = sagaTypeFullName.Split('.').Last();

                if (!Directory.Exists(tableName))
                {
                    Directory.CreateDirectory(tableName);
                }

                var table = client.GetTableReference(tableName);

                var query = new TableQuery<DictionaryTableEntity>();

                var regex = new Regex("Index_(?:.*)_(?<PropertyName>.*)_\"(?<PropertyValue>.*)\"#");
                var probablyJArrayRegex = new Regex("\\[.*\\]", RegexOptions.Multiline);
                var probablyJObjectRegex = new Regex("\\{.*\\}", RegexOptions.Multiline);

                await foreach (var entity in table.ExecuteQueryAsync(query, ct: cancellationToken))
                {
                    if (entity.PartitionKey.StartsWith("Index_"))
                    {
                        continue;
                    }

                    // NServiceBus_2ndIndexKey
                    // Index_OrderSagaData_OrderId_"a3413eda-fb98-46c1-a44e-89da9efada16"#

                    var match = regex.Match(entity["NServiceBus_2ndIndexKey"].StringValue);
                    var propertyName = match.Groups["PropertyName"].Value;
                    var propertyValue = match.Groups["PropertyValue"].Value;

                    entity.Remove("NServiceBus_2ndIndexKey");
                    entity.Remove("PartitionKey");
                    entity.Remove("RowKey");

                    var newSagaId = SagaIdGenerator.Generate(sagaTypeFullName, propertyName, propertyValue);
                    var oldSagaId = entity["Id"].GuidValue;

                    var root = new JObject();

                    // maybe somehow use the constants from the persister, currently hardcoded to move forward
                    var metadata = new JObject
                    {
                        { "SagaDataContainer-SchemaVersion", "1.0.0" },
                        { "SagaDataContainer-FullTypeName", sagaTypeFullName }, // just some random proposals to move forward
                        { "SagaDataContainer-OldSagaId", oldSagaId.ToString() } // just some random proposals to move forward
                    };
                    root.Add("_NServiceBus-Persistence-Metadata", metadata);

                    root.Add("_id", newSagaId);

                    foreach (var property in entity)
                    {
                        switch (property.Value.PropertyType)
                        {
                            case EdmType.String when probablyJArrayRegex.IsMatch(property.Value.StringValue):
                                try
                                {
                                    var propertyAsJArray = JArray.Parse(property.Value.StringValue);
                                    root.Add(property.Key, propertyAsJArray);
                                }
                                catch (JsonReaderException)
                                {
                                    // TODO: What to do here?
                                }

                                break;
                            case EdmType.String when probablyJObjectRegex.IsMatch(property.Value.StringValue):
                                try
                                {
                                    var propertyAsJObject = JObject.Parse(property.Value.StringValue);
                                    root.Add(property.Key, propertyAsJObject);
                                }
                                catch (JsonReaderException)
                                {
                                    // TODO: What to do here?
                                }
                                break;
                            case EdmType.Binary:
                                root.Add(property.Key, property.Value.BinaryValue);
                                break;
                            case EdmType.Boolean:
                                root.Add(property.Key, property.Value.BooleanValue);
                                break;
                            case EdmType.DateTime:
                                if (property.Value.DateTimeOffsetValue.HasValue)
                                {
                                    root.Add(property.Key, property.Value.DateTimeOffsetValue.Value);
                                }
                                else
                                {
                                    root.Add(property.Key, property.Value.DateTime);
                                }
                                break;
                            case EdmType.Double:
                                root.Add(property.Key, property.Value.DoubleValue);
                                break;
                            case EdmType.Guid:
                                root.Add(property.Key, property.Value.GuidValue);
                                break;
                            case EdmType.Int32:
                                root.Add(property.Key, property.Value.Int32Value);
                                break;
                            case EdmType.Int64:
                                root.Add(property.Key, property.Value.Int64Value);
                                break;
                            default:
                                root.Add(property.Key, property.Value.StringValue);
                                break;
                        }
                    }

                    await using var fileWriter = File.CreateText(Path.Combine(tableName, $"{newSagaId}.json"));
                    using var jsonTextWriter = new JsonTextWriter(fileWriter)
                    {
                        Formatting = Formatting.Indented
                    };
                    await root.WriteToAsync(jsonTextWriter, cancellationToken).ConfigureAwait(false);
                    await jsonTextWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await fileWriter.FlushAsync().ConfigureAwait(false);
                }
                return 0;
            });

            return await app.ExecuteAsync(args).ConfigureAwait(false);
        }
    }
}
