namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System.IO;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "migrate"
            };

            var verboseOption = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, true);
            var sagaDataNameOption = app.Option<string>($"-s|--{ApplicationOptions.SagaDataName}", "The saga data class name (w/o namespace) of the saga data to export. This will be the table name.", CommandOptionType.SingleValue);
            var connectionStringOption = app.Option<string>($"-c|--{ApplicationOptions.ConnectionString}", "The connection string to the Azure Storage account with the saga data.", CommandOptionType.SingleValue);

            app.HelpOption(inherited: true);

            app.OnExecuteAsync(cancellationToken =>
            {
                var logger = new ConsoleLogger(verboseOption.HasValue());

                return Exporter.Run(logger, connectionStringOption.Value(), sagaDataNameOption.Value(), Directory.GetCurrentDirectory(), cancellationToken);
            });

            return await app.ExecuteAsync(args).ConfigureAwait(false);
        }
    }
}
