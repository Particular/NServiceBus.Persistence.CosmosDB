namespace NServiceBus;

using System;
using Particular.Obsoletes;

public static partial class CosmosPersistenceConfig
{
    /// <summary>
    /// Enables using extracted container information from the incoming message. Without this setting, only extracted container information from incoming message headers will be used
    /// </summary>
    [ObsoleteMetadata(
            Message = "Using extracted container information from the incoming message will become the default behavior starting in version 4.0, making this API redundant",
            RemoveInVersion = "5",
            TreatAsErrorFromVersion = "4")]
    [Obsolete("Using extracted container information from the incoming message will become the default behavior starting in version 4.0, making this API redundant. Will be removed in version 5.0.0.", true)]
    public static PersistenceExtensions<CosmosPersistence> EnableContainerFromMessageExtractor(this PersistenceExtensions<CosmosPersistence> persistenceExtensions) => throw new NotImplementedException();
}
