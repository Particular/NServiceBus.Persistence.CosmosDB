namespace NServiceBus;

using System;
using Particular.Obsoletes;

public static partial class CosmosPersistenceConfig
{
    /// <summary>
    /// Enables using extracted container information from the incoming message. Without this setting, only extracted container information from incoming message headers will be used
    /// </summary>
    [ObsoleteMetadata(
            Message = "The EnableContainerFromMessageExtractor will become default behavior from v4.0",
            RemoveInVersion = "5",
            TreatAsErrorFromVersion = "4")]
    [Obsolete("The EnableContainerFromMessageExtractor will become default behavior from v4.0. Will be removed in version 5.0.0.", false)]
    public static PersistenceExtensions<CosmosPersistence> EnableContainerFromMessageExtractor(this PersistenceExtensions<CosmosPersistence> persistenceExtensions) => throw new NotImplementedException();
}
