namespace NServiceBus.Persistence.CosmosDB;

/// <summary>
/// Configuration for partition key extractors used by the Outbox.
/// </summary>
class ExtractorConfiguration
{
    public bool HasCustomPartitionHeaderExtractors { get; set; } = false;
    public bool HasCustomPartitionMessageExtractors { get; set; } = false;

    public bool HasAnyCustomPartitionExtractors => HasCustomPartitionHeaderExtractors || HasCustomPartitionMessageExtractors;

    public bool HasCustomContainerHeaderExtractors { get; set; } = false;
    public bool HasCustomContainerMessageExtractors { get; set; } = false;

    public bool HasAnyCustomContainerExtractors => HasCustomContainerHeaderExtractors || HasCustomContainerMessageExtractors;
}