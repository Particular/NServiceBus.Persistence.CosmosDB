namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Outbox;
    using Persistence.CosmosDB;

    /// <summary>
    /// Cosmos DB outbox settings
    /// </summary>
    public static class CosmosOutboxSettingsExtensions
    {
        /// <summary>
        /// Sets the time to live for outbox deduplication records
        /// </summary>
        public static void TimeToKeepOutboxDeduplicationData(this OutboxSettings outboxSettings, TimeSpan timeToKeepOutboxDeduplicationData)
        {
            Guard.AgainstNegativeAndZero(nameof(timeToKeepOutboxDeduplicationData), timeToKeepOutboxDeduplicationData);

            outboxSettings.GetSettings().Set(SettingsKeys.OutboxTimeToLiveInSeconds, (int)timeToKeepOutboxDeduplicationData.TotalSeconds);
        }
    }
}