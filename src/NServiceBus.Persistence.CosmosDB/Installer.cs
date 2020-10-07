namespace NServiceBus.Persistence.CosmosDB
{
    using System;
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

        public async Task Install(string identity)
        {
            if (installerSettings == null || installerSettings.Disabled)
            {
                return;
            }

            try
            {
                await CreateContainerIfNotExists().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.Error("Could not complete the installation. ", e);
                throw;
            }
        }

        async Task CreateContainerIfNotExists()
        {
            await clientProvider.Client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName)
                .ConfigureAwait(false);

            var database = clientProvider.Client.GetDatabase(installerSettings.DatabaseName);

            var containerProperties = new ContainerProperties(installerSettings.ContainerName, installerSettings.PartitionKeyPath);

            await database.CreateContainerIfNotExistsAsync(containerProperties)
                .ConfigureAwait(false);
        }

        InstallerSettings installerSettings;
        static ILog log = LogManager.GetLogger<Installer>();
        readonly IProvideCosmosClient clientProvider;
    }
}