﻿namespace NServiceBus.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_custom_container_information_extractor_from_message_registered_via_api : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_be_used()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithCustomExtractor>(b => b.When(session => session.SendLocal(new StartSaga1 { DataId = Guid.NewGuid() })))
            .Done(c => c.SagaReceivedMessage)
            .Run(runSettings);

        Assert.That(context.ExtractorWasCalled, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool SagaReceivedMessage { get; set; }
        public bool ExtractorWasCalled { get; set; }
    }

    public class EndpointWithCustomExtractor : EndpointConfigurationBuilder
    {
        public EndpointWithCustomExtractor() =>
            EndpointSetup<DefaultServer>((config, r) =>
            {
                PersistenceExtensions<CosmosPersistence> persistence = config.UsePersistence<CosmosPersistence>();
                TransactionInformationConfiguration transactionInformation = persistence.TransactionInformation();
                transactionInformation.ExtractContainerInformationFromMessage(new CustomExtractor((Context)r.ScenarioContext));
            });

        public class JustASaga(Context testContext) : Saga<JustASagaData>, IAmStartedByMessages<StartSaga1>
        {
            public Task Handle(StartSaga1 message, IMessageHandlerContext context)
            {
                Data.DataId = message.DataId;
                testContext.SagaReceivedMessage = true;
                MarkAsComplete();
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper) => mapper.ConfigureMapping<StartSaga1>(m => m.DataId).ToSaga(s => s.DataId);
        }

        public class CustomExtractor(Context testContext) : IContainerInformationFromMessagesExtractor
        {
            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                testContext.ExtractorWasCalled = true;
                return true;
            }
        }

        public class JustASagaData : ContainSagaData
        {
            public virtual Guid DataId { get; set; }
        }
    }

    public class StartSaga1 : ICommand
    {
        public Guid DataId { get; set; }
    }
}