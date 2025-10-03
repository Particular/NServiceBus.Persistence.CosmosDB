namespace NServiceBus.Persistence.CosmosDB;

using System;

sealed class OutboxPersistenceConfiguration
{
    public TimeSpan TimeToKeepDeduplicationData
    {
        get => timeToKeepDeduplicationData;
        set
        {
            Guard.AgainstNegativeAndZero(nameof(value), value);

            var seconds = Math.Ceiling(value.TotalSeconds);
            timeToKeepDeduplicationData = TimeSpan.FromSeconds(seconds);
        }
    }
    TimeSpan timeToKeepDeduplicationData = TimeSpan.FromDays(7);

    public bool ReadFallbackEnabled { get; set; } = true; // default to true to not break existing users

    public string PartitionKey { get; set; } = null!; // will be set by defaults
}