namespace NServiceBus;

using Features;
using Persistence;
using Persistence.CosmosDB;

/// <summary>
/// Used to configure NServiceBus to use Cosmos DB persistence.
/// </summary>
public class CosmosPersistence : PersistenceDefinition, IPersistenceDefinitionFactory<CosmosPersistence>
{
    CosmosPersistence()
    {
        Defaults(s =>
        {
            s.SetDefault(SettingsKeys.DatabaseName, "NServiceBus");
            s.SetDefault<IProvideCosmosClient>(new ThrowIfNoCosmosClientIsProvided());
            s.EnableFeature<InstallerFeature>();
            s.EnableFeature<Transaction>();
        });

        Supports<StorageType.Sagas, SagaStorage>();
        Supports<StorageType.Outbox, OutboxStorage>();
    }

    static CosmosPersistence IPersistenceDefinitionFactory<CosmosPersistence>.Create() => new();
}