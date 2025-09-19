namespace NServiceBus.Persistence.CosmosDB;

/// <summary>
/// Holds the ExtractorConfiguration that is populated by the pipeline behaviors
/// and accessed by the OutboxPersister and LogicalOutboxBehavior.
/// </summary>
class ExtractorConfigurationHolder
{
    public ExtractorConfiguration Configuration { get; set; }
}