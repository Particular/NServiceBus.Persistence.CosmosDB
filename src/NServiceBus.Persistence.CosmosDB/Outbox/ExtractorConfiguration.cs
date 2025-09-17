namespace NServiceBus.Persistence.CosmosDB;

/// <summary>
/// Configuration for partition key extractors used by the Outbox.
/// </summary>
class ExtractorConfiguration
{
    public bool HasCustomHeaderExtractors { get; init; } = false;
    public bool HasCustomMessageExtractors { get; init; } = false;

    public bool HasAnyCustomExtractors => HasCustomHeaderExtractors || HasCustomMessageExtractors;
}