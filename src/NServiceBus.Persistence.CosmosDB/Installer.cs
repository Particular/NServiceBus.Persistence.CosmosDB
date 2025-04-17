namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Installation;
using Logging;
using Microsoft.Azure.Cosmos;

class Installer : INeedToInstallSomething
{
    public Installer(IProvideCosmosClient clientProvider, InstallerSettings settings)
    {
        installerSettings = settings;
        this.clientProvider = clientProvider;
    }

    public async Task Install(string identity, CancellationToken cancellationToken = default)
    {
        if (installerSettings == null || installerSettings.Disabled)
        {
            return;
        }

        try
        {
            await CreateContainerIfNotExists(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            log.Error("Could not complete the installation. ", e);
            throw;
        }
    }

    async Task CreateContainerIfNotExists(CancellationToken cancellationToken)
    {
        await clientProvider.Client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Database database = clientProvider.Client.GetDatabase(installerSettings.DatabaseName);

        var containerProperties =
            new ContainerProperties(installerSettings.ContainerName, installerSettings.PartitionKeyPath)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    InstallerSettings installerSettings;
    static ILog log = LogManager.GetLogger<Installer>();
    readonly IProvideCosmosClient clientProvider;
}