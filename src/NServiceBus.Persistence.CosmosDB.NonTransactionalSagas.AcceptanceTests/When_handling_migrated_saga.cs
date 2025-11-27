namespace NServiceBus.AcceptanceTests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NServiceBus;
using AcceptanceTesting;
using NUnit.Framework;
using Persistence.CosmosDB;
using Headers = Headers;

class When_handling_migrated_saga : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_work() =>
        await Scenario.Define<Context>()
            .WithEndpoint<MigratingEndpoint>(b => b.When(async (s, c) =>
            {
                await ImportIntoCosmosDB(c);
                var options = new SendOptions();
                options.RouteToThisEndpoint();
                options.SetHeader(Headers.SagaId, c.MigratedSagaId.ToString());
                await s.Send(new CompleteSagaResponse(), options);
            }))
            .Done(ctx => ctx.CompleteSagaResponseReceived)
            .Run();

    static string MigrationDocument = @"{{
    ""_NServiceBus-Persistence-Metadata"": {{
        ""SagaDataContainer-SchemaVersion"": ""1.0.0"",
        ""SagaDataContainer-FullTypeName"": ""NServiceBus.AcceptanceTests.When_handling_migrated_saga+MigratingEndpoint+MigratingFromAsp2SagaData"",
        ""SagaDataContainer-MigratedSagaId"": ""{0}""
    }},
    ""id"": ""{1}"",
    ""MyId"": ""{2}"",
    ""Originator"": ""Migrationendtoend.MigratingEndpoint"",
    ""OriginalMessageId"": ""6492af3f-1d60-43f6-8e62-ae1600ab23a2""
}}";

    static async Task ImportIntoCosmosDB(Context scenarioContext)
    {
        Microsoft.Azure.Cosmos.Container container = SetupFixture.Container;

        var actualSagaId = CosmosSagaIdGenerator.Generate(typeof(MigratingEndpoint.MigratingFromAsp2SagaData),
            nameof(MigratingEndpoint.MigratingFromAsp2SagaData.MyId), scenarioContext.MyId);

        string document = string.Format(MigrationDocument, scenarioContext.MigratedSagaId, actualSagaId, scenarioContext.MyId);
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(document)))
        {
            Microsoft.Azure.Cosmos.ResponseMessage response = await container.CreateItemStreamAsync(stream, new PartitionKey(actualSagaId.ToString()));

            Assert.That(response.IsSuccessStatusCode, Is.True, "Successfully imported");
        }
    }

    public class Context : ScenarioContext
    {
        public bool CompleteSagaResponseReceived { get; set; }

        public Guid MyId { get; } = Guid.NewGuid();
        public Guid MigratedSagaId { get; } = Guid.NewGuid();
    }

    public class MigratingEndpoint : EndpointConfigurationBuilder
    {
        public MigratingEndpoint() => EndpointSetup<DefaultServer>(ec =>
        {
            var persistence = ec.UsePersistence<CosmosPersistence>();
            persistence.Sagas().EnableMigrationMode();
        });

        public class MigratingSaga(Context testContext) : Saga<MigratingFromAsp2SagaData>,
            IAmStartedByMessages<StartSaga>,
            IHandleMessages<CompleteSagaResponse>
        {
            // This code path will never be executed
            public Task Handle(StartSaga message, IMessageHandlerContext context)
            {
                Data.MyId = message.MyId;
                return Task.CompletedTask;
            }

            public Task Handle(CompleteSagaResponse message, IMessageHandlerContext context)
            {
                testContext.CompleteSagaResponseReceived = true;

                MarkAsComplete();
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MigratingFromAsp2SagaData> mapper) =>
                mapper.MapSaga(saga => saga.MyId).ToMessage<StartSaga>(msg => msg.MyId);
        }

        public class MigratingFromAsp2SagaData : ContainSagaData
        {
            public Guid MyId { get; set; }
        }
    }

    public class StartSaga : ICommand
    {
        public Guid MyId { get; set; }
    }

    public class CompleteSagaResponse : IMessage
    {
    }
}