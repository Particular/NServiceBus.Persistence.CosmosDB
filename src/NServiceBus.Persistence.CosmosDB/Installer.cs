namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Installation;
using Logging;
using Microsoft.Azure.Cosmos;

class Installer(IProvideCosmosClient clientProvider, InstallerSettings settings)
    : INeedToInstallSomething
{
    public async Task Install(string identity, CancellationToken cancellationToken = default)
    {
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
        await clientProvider.Client.CreateDatabaseIfNotExistsAsync(settings.DatabaseName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Database database = clientProvider.Client.GetDatabase(settings.DatabaseName);

        var containerProperties =
            new ContainerProperties(settings.ContainerInformation.ContainerName, settings.ContainerInformation.PartitionKeyPath)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    static ILog log = LogManager.GetLogger<Installer>();
}