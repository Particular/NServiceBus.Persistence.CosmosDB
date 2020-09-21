namespace Particular.AzureStoragePersistenceSagaExporter
{
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
            var sagaDataTypeOption = app.Option<string>($"-t|--{ApplicationOptions.SagaDataFullTypeName}", "The full type name (namespace + type) of the saga data to export", CommandOptionType.SingleValue);
            var connectionStringOption = app.Option<string>($"-c|--{ApplicationOptions.ConnectionString}", "The connection string to the Azure Storage account with the saga data.", CommandOptionType.SingleValue);

            app.HelpOption(inherited: true);

            app.OnExecuteAsync(cancellationToken =>
            {
                var logger = new ConsoleLogger(verboseOption.HasValue());

                return Exporter.Run(logger, connectionStringOption.Value(), sagaDataTypeOption.Value(), cancellationToken);
            });

            return await app.ExecuteAsync(args).ConfigureAwait(false);
        }
    }
}
