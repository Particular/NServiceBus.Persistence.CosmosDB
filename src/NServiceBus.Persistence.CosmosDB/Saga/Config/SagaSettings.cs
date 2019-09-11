namespace NServiceBus.Persistence.CosmosDB
{
    using Settings;

    /// <summary>
    /// Configuration options for Saga persistence.
    /// </summary>
    public class SagaSettings
    {
        SettingsHolder settings;

        internal SagaSettings(SettingsHolder settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Default collection name to be used
        /// </summary>
        public void DefaultCollectionName(string collectionName)
        {
            Guard.AgainstNullAndEmpty(nameof(collectionName), collectionName);

            settings.Set("CosmosDB.DefaultCollectionName", collectionName);
        }
    }
}