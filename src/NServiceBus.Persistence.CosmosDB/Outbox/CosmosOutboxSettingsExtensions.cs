namespace NServiceBus;

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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToKeepOutboxDeduplicationData, TimeSpan.Zero);

        outboxSettings.GetSettings().GetOrCreate<OutboxPersistenceConfiguration>().TimeToKeepDeduplicationData = timeToKeepOutboxDeduplicationData;
    }

    /// <summary>
    /// When retrieving outbox messages, the persister tries to load the outbox records assuming the new or the old
    /// outbox record schema. For collections that are known to only contain the new schema this fallback can be disabled.
    /// </summary>
    public static OutboxSettings DisableReadFallback(this OutboxSettings outboxSettings)
    {
        ArgumentNullException.ThrowIfNull(outboxSettings);

        outboxSettings.GetSettings().GetOrCreate<OutboxPersistenceConfiguration>().ReadFallbackEnabled = false;

        return outboxSettings;
    }
}