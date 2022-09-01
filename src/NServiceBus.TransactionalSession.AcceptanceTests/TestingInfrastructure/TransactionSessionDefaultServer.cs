namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using Persistence.CosmosDB.TransactionalSession;

public class TransactionSessionDefaultServer : DefaultServer
{
    public static Action<EndpointConfiguration> ConfigurePersistence { get; set; } =
        _ => throw new NotImplementedException();

    public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor,
        EndpointCustomizationConfiguration endpointConfiguration,
        Action<EndpointConfiguration> configurationBuilderCustomization)
    {
        endpointConfiguration.TypesToInclude.Add(typeof(CaptureBuilderFeature));
        endpointConfiguration.TypesToInclude.Add(typeof(CosmosDbTransactionalSessionFeature));

        return base.GetConfiguration(runDescriptor, endpointConfiguration, configuration =>
        {
            configuration.EnableTransactionalSession();

            configuration.EnableFeature<CaptureBuilderFeature>();

            ConfigurePersistence(configuration);

            configurationBuilderCustomization(configuration);
        });
    }
}