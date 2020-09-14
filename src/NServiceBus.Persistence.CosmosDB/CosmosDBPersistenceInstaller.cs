namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Installation;
    using Logging;
    using Microsoft.Azure.Cosmos;

    class CosmosDBPersistenceInstaller : INeedToInstallSomething
    {
        public CosmosDBPersistenceInstaller(ClientHolder clientHolder, InstallerSettings installerSettings)
        {
            this.clientHolder = clientHolder;
            this.installerSettings = installerSettings;
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
            await clientHolder.Client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName)
                .ConfigureAwait(false);

            var database = clientHolder.Client.GetDatabase(installerSettings.DatabaseName);

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

            // TODO should we check if saga feature is active?
            containerProperties.UniqueKeyPolicy.UniqueKeys.Add(new UniqueKey
            {
                Paths = {"/Id"}
            });

            await database.CreateContainerIfNotExistsAsync(containerProperties)
                .ConfigureAwait(false);
        }


        readonly ClientHolder clientHolder;
        readonly InstallerSettings installerSettings;
        static ILog log = LogManager.GetLogger<CosmosDBPersistenceInstaller>();
    }
}