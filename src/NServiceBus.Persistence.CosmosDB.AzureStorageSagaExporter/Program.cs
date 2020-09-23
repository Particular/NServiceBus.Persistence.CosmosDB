namespace NServiceBus.Persistence.CosmosDB.AzureStorageSagaExporter
{
    using System.IO;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "migrate"
            };

            var verboseOption = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, true);
            var versionOption = app.Option("--version", "Show the current version of the tool", CommandOptionType.NoValue, true);
            var sagaDataNameOption = app.Option<string>($"-s|--{ApplicationOptions.SagaDataName}", "The saga data class name (w/o namespace) of the saga data to export. This will be the table name.", CommandOptionType.SingleValue);
            var connectionStringOption = app.Option<string>($"-c|--{ApplicationOptions.ConnectionString}", "The connection string to the Azure Storage account with the saga data.", CommandOptionType.SingleValue);

            app.HelpOption(inherited: true);

            app.OnExecuteAsync(async cancellationToken =>
            {
                var logger = new ConsoleLogger(verboseOption.HasValue());

                logger.LogInformation(ToolVersion.GetVersionInfo());

                if (versionOption.HasValue())
                {
                    return;
                }

                if (!await ToolVersion.CheckIsLatestVersion(logger).ConfigureAwait(false))
                {
                    return;
                }

                await Exporter.Run(logger, connectionStringOption.Value(), sagaDataNameOption.Value(), Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);
            });

            return await app.ExecuteAsync(args).ConfigureAwait(false);
        }
    }
}