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
        /// Connection string to use for sagas storage.
        /// </summary>
        public void ConnectionString(string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            // TODO: should we assume a single connection string rather than multiple connection strings (per persistence type?)
            settings.Set(WellKnownConfigurationKeys.SagasConnectionString, connectionString);
        }

        /// <summary>
        /// Database name to be used
        /// </summary>
        public void DatabaseName(string databaseName)
        {
            Guard.AgainstNullAndEmpty(nameof(databaseName), databaseName);

            settings.Set(WellKnownConfigurationKeys.SagasDatabaseName, databaseName);
        }

        /// <summary>
        /// Container name to be used
        /// </summary>
        public void ContainerName(string containerName)
        {
            Guard.AgainstNullAndEmpty(nameof(containerName), containerName);

            settings.Set(WellKnownConfigurationKeys.SagasContainerName, containerName);
        }
    }
}