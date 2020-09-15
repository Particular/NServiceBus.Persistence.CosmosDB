using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;
using NServiceBus.ObjectBuilder;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Persistence.CosmosDB.Outbox;
using NServiceBus.Pipeline;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        if (configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
        {
            return Task.FromResult(0);
        }

        var persistence = configuration.UsePersistence<CosmosDbPersistence>();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        persistence.Container(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }

    class PartitionKeyProviderFeature : Feature
    {
        public PartitionKeyProviderFeature()
        {
            EnableByDefault();
            DependsOn(nameof(OutboxStorage));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register(new PartitionKeyProviderBehavior.PartitionKeyProviderBehaviorRegisterStep());
        }
    }

    class PartitionKeyProviderBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        ScenarioContext scenarioContext;

        public PartitionKeyProviderBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            context.Extensions.Set(new PartitionKey(scenarioContext.TestRunId.ToString()));
            return next();
        }

        public class PartitionKeyProviderBehaviorRegisterStep : RegisterStep
        {
            public PartitionKeyProviderBehaviorRegisterStep() : base(nameof(PartitionKeyProviderBehavior), typeof(PartitionKeyProviderBehavior), "Populates the partition key", b => new PartitionKeyProviderBehavior(b.Build<ScenarioContext>()))
            {
                InsertBefore("LogicalOutboxBehavior");
            }
        }
    }
}