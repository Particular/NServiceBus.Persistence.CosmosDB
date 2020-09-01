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
    }
}