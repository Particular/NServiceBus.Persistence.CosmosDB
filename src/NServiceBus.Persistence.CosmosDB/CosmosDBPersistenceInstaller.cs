﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Installation;
    using Logging;
    using Microsoft.Azure.Cosmos;
    using Settings;

    class CosmosDBPersistenceInstaller : INeedToInstallSomething
    {
        public CosmosDBPersistenceInstaller(ReadOnlySettings settings)
        {
            installerSettings = settings.GetOrDefault<InstallerSettings>();
        }

        public async Task Install(string identity)
        {
            if (installerSettings == null || installerSettings.Disabled)
            {
                return;
            }

            try
            {
                await CreateContainerIfNotExists(installerSettings).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.Error("Could not complete the installation. ", e);
                throw;
            }
        }

        internal static async Task CreateContainerIfNotExists(InstallerSettings installerSettings)
        {
            // TODO we can probably assume the client can be injected
            await installerSettings.Client.CreateDatabaseIfNotExistsAsync(installerSettings.DatabaseName)
                .ConfigureAwait(false);

            var database = installerSettings.Client.GetDatabase(installerSettings.DatabaseName);

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

        InstallerSettings installerSettings;
        static ILog log = LogManager.GetLogger<CosmosDBPersistenceInstaller>();
    }
}