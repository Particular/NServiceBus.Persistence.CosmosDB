﻿using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Pipeline;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        if (configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
        {
            return Task.FromResult(0);
        }

        var persistence = configuration.UsePersistence<CosmosPersistence>();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        // This populates the partition key at the physical stage to test the conventional outbox use-case
        configuration.Pipeline.Register(typeof(PartitionKeyProviderBehavior), "Populates the partition key");

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }

    class PartitionKeyProviderBehavior : Behavior<ITransportReceiveContext>
    {
        ScenarioContext scenarioContext;

        public PartitionKeyProviderBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            context.Extensions.Set(new PartitionKey(scenarioContext.TestRunId.ToString()));
            context.Extensions.Set(new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)));
            return next();
        }
    }
}