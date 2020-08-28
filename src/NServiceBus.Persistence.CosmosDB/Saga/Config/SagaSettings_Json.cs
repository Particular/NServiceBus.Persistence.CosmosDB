namespace NServiceBus.Persistence.CosmosDB
{
    using Newtonsoft.Json;
    using Settings;

    public partial class SagaSettings
    {
        /// <summary>
        /// The <see cref="JsonSerializerSettings"/> to use for serializing sagas.
        /// </summary>
        public void JsonSettings(JsonSerializerSettings jsonSerializerSettings)
        {
            settings.Set(SettingsKeys.Sagas.JsonSerializerSettings, jsonSerializerSettings);
        }

        internal static JsonSerializerSettings GetJsonSerializerSettings(ReadOnlySettings settings)
        {
            return settings.GetOrDefault<JsonSerializerSettings>(SettingsKeys.Sagas.JsonSerializerSettings);
        }
    }
}