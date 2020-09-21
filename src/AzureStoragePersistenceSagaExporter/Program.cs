namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Cosmos.Table;

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

                var table = client.GetTableReference(tableName);

                var query = new TableQuery<DictionaryTableEntity>();

                await foreach (var entity in table.ExecuteQueryAsync(query, ct: cancellationToken))
                {
                    if (entity.PartitionKey.StartsWith("Index_"))
                    {
                        continue;
                    }
                }

                //Each tableEntity
                //  - Record primary key as old saga id
                //  - Calc new saga id - parse secondary index
                //  - To JObject
                //  - Detect and convert string values that are serialized objects/arrays
                //  - Remove PartitionKey/RowKey/secondary index properties
                //  - Add metadata (old saga id / saga full type name)
                //  - Dump file to current folder

                return 0;
            });

            return await app.ExecuteAsync(args).ConfigureAwait(false);
        }
    }
}
