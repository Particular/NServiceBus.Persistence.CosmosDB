namespace NServiceBus.Persistence.CosmosDB;

/// <summary>
/// Configuration for partition key extractors used by the Outbox.
/// </summary>
class ExtractorConfiguration
{
    public bool HasCustomPartitionHeaderExtractors { get; init; } = false;
    public bool HasCustomPartitionMessageExtractors { get; init; } = false;

    public bool HasAnyCustomPartitionExtractors => HasCustomPartitionHeaderExtractors || HasCustomPartitionMessageExtractors;

    public bool HasCustomContainerHeaderExtractors { get; init; } = false;
    public bool HasCustomContainerMessageExtractors { get; init; } = false;

    public bool HasAnyCustomContainerExtractors => HasCustomContainerHeaderExtractors || HasCustomContainerMessageExtractors;
}