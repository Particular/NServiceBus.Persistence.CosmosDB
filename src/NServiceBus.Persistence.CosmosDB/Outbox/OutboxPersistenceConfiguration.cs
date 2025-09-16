namespace NServiceBus.Persistence.CosmosDB;

using System;

sealed class OutboxPersistenceConfiguration
{
    public TimeSpan TimeToKeepDeduplicationData
    {
        get => field;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

            var seconds = Math.Ceiling(value.TotalSeconds);
            field = TimeSpan.FromSeconds(seconds);
        }
    } = TimeSpan.FromDays(7);

    public bool ReadFallbackEnabled { get; set; } = true; // default to true to not break existing users

    public string PartitionKey { get; set; } = null!; // will be set by defaults
}