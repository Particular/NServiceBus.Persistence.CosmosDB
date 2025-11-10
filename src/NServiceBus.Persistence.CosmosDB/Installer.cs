namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Installation;
using Logging;
using Microsoft.Azure.Cosmos;
using Settings;

class Installer(IProvideCosmosClient clientProvider, IReadOnlySettings settings)
    : INeedToInstallSomething
{
    public async Task Install(string identity, CancellationToken cancellationToken = default)
    {
        var installerSettings = settings.Get<InstallerSettings>();

        string databaseName = settings.Get<string>(SettingsKeys.DatabaseName);

        if (!settings.TryGet(out ContainerInformation containerInformation))
        {
            return;
        }

        installerSettings.ContainerName = containerInformation.ContainerName;
        installerSettings.DatabaseName = databaseName;
        installerSettings.PartitionKeyPath = containerInformation.PartitionKeyPath;

        try
        {
            await CreateContainerIfNotExists(installerSettings, clientProvider.Client, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            Log.Error("Could not complete the installation. ", e);
            throw;
        }
    }

    internal static async Task CreateContainerIfNotExists(InstallerSettings installerSettings, CosmosClient client, CancellationToken cancellationToken = default)
    {
        await client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Database database = client.GetDatabase(installerSettings.DatabaseName);

        var containerProperties =
            new ContainerProperties(installerSettings.ContainerName, installerSettings.PartitionKeyPath)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    static readonly ILog Log = LogManager.GetLogger<Installer>();
}