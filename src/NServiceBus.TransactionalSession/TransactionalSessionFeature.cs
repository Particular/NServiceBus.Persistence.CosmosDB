namespace NServiceBus.Persistence.CosmosDB.TransactionalSession;

using Features;
using NServiceBus.TransactionalSession;
using SynchronizedStorage = SynchronizedStorage;

/// <summary>
///
/// </summary>
public class CosmosDbTransactionalSessionFeature : Feature
{
    /// <summary>
    ///
    /// </summary>
    public CosmosDbTransactionalSessionFeature()
    {
        EnableByDefault();
        DependsOn<SynchronizedStorage>();
        DependsOn<TransactionalSessionFeature>();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Container.ConfigureComponent<TransactionalSessionExtension>(DependencyLifecycle.InstancePerUnitOfWork);
        context.Pipeline.Register(new CosmosControlMessageBehavior(), "Propagates control message header values to PartitionKeys and ContainerInformation when necessary.");
    }
}