namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Installation;
    using Logging;
    using Microsoft.Azure.Cosmos;

    class CosmosDBPersistenceInstaller : INeedToInstallSomething
    {
        public CosmosDBPersistenceInstaller(IProvideCosmosClient clientProvider, InstallerSettings settings)
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
            // TODO we can probably assume the client can be injected
            await clientProvider.Client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName)
                .ConfigureAwait(false);

            var database = clientProvider.Client.GetDatabase(installerSettings.DatabaseName);

            // TODO: Should we sanity check properties?
            // var container = database.GetContainer(containerName);
            // try
            // {
            //     var response = await container.ReadContainerAsync().ConfigureAwait(false);
            //     containerProperties = response.Resource;
            //     // currently not checking if things are actually coherent. We probably should
            //     if (containerProperties != null)
            //     {
            //         break;
            //     }
            // }
            // catch (CosmosException)
            // {
            //     break;
            // }

            var containerProperties = new ContainerProperties(installerSettings.ContainerName, installerSettings.PartitionKeyPath);

            await database.CreateContainerIfNotExistsAsync(containerProperties)
                .ConfigureAwait(false);
        }

        InstallerSettings installerSettings;
        static ILog log = LogManager.GetLogger<CosmosDBPersistenceInstaller>();
        readonly IProvideCosmosClient clientProvider;
    }
}