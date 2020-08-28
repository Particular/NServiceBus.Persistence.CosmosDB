namespace NServiceBus.Persistence.CosmosDB
{
    using Settings;

    /// <summary>
    /// Configuration options for Saga persistence.
    /// </summary>
    public partial class SagaSettings
    {
        SettingsHolder settings;

        internal SagaSettings(SettingsHolder settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Database name to be used
        /// </summary>
        public void DatabaseName(string databaseName)
        {
            Guard.AgainstNullAndEmpty(nameof(databaseName), databaseName);

            settings.Set(SettingsKeys.Sagas.DatabaseName, databaseName);
        }

        /// <summary>
        /// Container name to be used
        /// </summary>
        public void ContainerName(string containerName)
        {
            Guard.AgainstNullAndEmpty(nameof(containerName), containerName);

            settings.Set(SettingsKeys.Sagas.ContainerName, containerName);
        }
    }
}