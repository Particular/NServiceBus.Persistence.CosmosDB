namespace NServiceBus.Persistence.CosmosDB.AzureStorageSagaExporter
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    using LogLevel = NuGet.Common.LogLevel;

    public class ToolVersion
    {
        const string PackageID = "Particular.Asp.Export";
        const string FeedUri = "https://www.myget.org/F/particular/api/v3/index.json";

        public static string GetVersionInfo()
        {
            return $"export-aspsagas {GitVersionInformation.NuGetVersionV2} (Sha:{GitVersionInformation.ShortSha})";
        }

        public static async Task<bool> CheckIsLatestVersion(ILogger logger, bool ignoreUpdates, CancellationToken cancellationToken = default)
        {
            try
            {
                var nugetLogger = new LoggerAdapter(logger);

                var cache = new SourceCacheContext();
                var packageSource = new PackageSource(FeedUri);
                var repository = new SourceRepository(packageSource, Repository.Provider.GetCoreV3());

                var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
                var versions = await resource.GetAllVersionsAsync(PackageID, cache, nugetLogger, cancellationToken).ConfigureAwait(false);

                var current = new NuGetVersion(GitVersionInformation.NuGetVersionV2);
                var latest = versions.OrderByDescending(pkg => pkg.Version).FirstOrDefault() ?? current;

                if (latest > current)
                {
                    var packageVersion = latest.ToNormalizedString();

                    log($"*** New version detected: {packageVersion}");
                    log("*** Update to the latest version using the following command:");
                    log($"***   dotnet tool update --tool-path <installation-path> {PackageID} --add-source {FeedUri} --version {packageVersion}");

                    return ignoreUpdates;

                    void log(string message)
                    {
                        if (ignoreUpdates)
                        {
                            logger.LogInformation(message);
                        }
                        else
                        {
                            logger.LogCritical(message);
                        }
                    }
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException) && cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("*** Unable to connect to MyGet to check for latest version.");
                logger.LogWarning($"*** Message: {e.Message}");
            }

            return true;
        }

        class LoggerAdapter : NuGet.Common.ILogger
        {
            readonly ILogger loggerImplementation;

            public LoggerAdapter(ILogger logger)
            {
                loggerImplementation = logger;
            }

            public void LogDebug(string data) => loggerImplementation.LogDebug(data);

            public void LogVerbose(string data) => loggerImplementation.LogDebug(data);

            public void LogInformation(string data) => loggerImplementation.LogInformation(data);

            public void LogMinimal(string data) { }

            public void LogWarning(string data) => loggerImplementation.LogWarning(data);

            public void LogError(string data) => loggerImplementation.LogError(data);

            public void LogInformationSummary(string data) => loggerImplementation.LogInformation(data);

            public void Log(LogLevel level, string data) { }

            public Task LogAsync(LogLevel level, string data) => Task.CompletedTask;

            public void Log(ILogMessage message) { }

            public Task LogAsync(ILogMessage message) => Task.CompletedTask;
        }
    }
}