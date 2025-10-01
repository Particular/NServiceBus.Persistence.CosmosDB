namespace NServiceBus.Persistence.CosmosDB;

using System;

sealed class OutboxPersistenceConfiguration
{
    TimeSpan timeToKeepDeduplicationData = TimeSpan.FromDays(7);
    public TimeSpan TimeToKeepDeduplicationData
    {
        get => timeToKeepDeduplicationData;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

            var seconds = Math.Ceiling(value.TotalSeconds);
            timeToKeepDeduplicationData = TimeSpan.FromSeconds(seconds);
        }
    }

    public bool ReadFallbackEnabled { get; set; } = true; // default to true to not break existing users

    public string PartitionKey { get; set; } = null!; // will be set by defaults
}