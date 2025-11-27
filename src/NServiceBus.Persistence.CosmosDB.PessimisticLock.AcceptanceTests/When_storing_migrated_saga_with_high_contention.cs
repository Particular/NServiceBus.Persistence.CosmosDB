namespace NServiceBus.AcceptanceTests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Persistence.CosmosDB;
using Headers = Headers;

[TestFixture]
public class When_storing_migrated_saga_with_high_contention : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_succeed_without_retries()
    {
        HighContentionScenario context = await Scenario.Define<HighContentionScenario>()
            .WithEndpoint<HighContentionEndpointWithMigrationMode>(behavior =>
            {
                behavior.When(async (session, scenarioContext) =>
                {
                    await ImportIntoCosmosDB(scenarioContext);
                    await Task.WhenAll(Enumerable.Range(0, scenarioContext.ConcurrentMessageCount).Select(_ =>
                    {
                        var options = new SendOptions();
                        options.RouteToThisEndpoint();
                        options.SetHeader(Headers.SagaId, scenarioContext.MigratedSagaId.ToString());
                        return session.Send(new ConcurrentMessage { SomeId = scenarioContext.SomeId }, options);
                    }));
                    scenarioContext.ConcurrentMessagesSent = true;
                });
            })
            .Done(s => s.SagaCompleted)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.ConcurrentMessagesSent, Is.True);
            Assert.That(context.RetryCount, Is.EqualTo(0));
        });
    }

    static string MigrationDocument = @"{{
    ""_NServiceBus-Persistence-Metadata"": {{
        ""SagaDataContainer-SchemaVersion"": ""1.0.0"",
        ""SagaDataContainer-FullTypeName"": ""NServiceBus.AcceptanceTests.When_storing_migrated_saga_with_high_contention+HighContentionEndpoint+HighContentionSaga"",
        ""SagaDataContainer-MigratedSagaId"": ""{0}""
    }},
    ""id"": ""{1}"",
    ""SomeId"": ""{2}"",
    ""HitCount"": 0,
    ""Originator"": ""NServiceBus.AcceptanceTests.HighContentionEndpoint"",
    ""OriginalMessageId"": ""6492af3f-1d60-43f6-8e62-ae1600ab23a2""
}}";

    static async Task ImportIntoCosmosDB(HighContentionScenario scenarioContext)
    {
        Container container = SetupFixture.Container;

        Guid actualSagaId = CosmosSagaIdGenerator.Generate(typeof(HighContentionEndpointWithMigrationMode.HighContentionSaga),
            nameof(HighContentionEndpointWithMigrationMode.HighContentionSaga.HighContentionSagaData.SomeId), scenarioContext.SomeId);

        string document = string.Format(MigrationDocument, scenarioContext.MigratedSagaId, actualSagaId, scenarioContext.SomeId);
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(document)))
        {
            ResponseMessage response = await container.CreateItemStreamAsync(stream, new PartitionKey(actualSagaId.ToString()));

            Assert.That(response.IsSuccessStatusCode, Is.True, "Successfully imported");
        }
    }

    public class HighContentionScenario : ScenarioContext
    {
        long retryCount;

        public int ConcurrentMessageCount => 20;

        public bool ConcurrentMessagesSent { get; set; }

        public bool SagaCompleted { get; set; }

        public Guid SomeId { get; } = Guid.NewGuid();

        public Guid MigratedSagaId { get; } = Guid.NewGuid();

        public long RetryCount => Interlocked.Read(ref retryCount);

        public void IncrementRetryCount() => Interlocked.Increment(ref retryCount);
    }

    class HighContentionEndpointWithMigrationMode : EndpointConfigurationBuilder
    {
        public HighContentionEndpointWithMigrationMode() =>
            EndpointSetup<DefaultServer, HighContentionScenario>((endpoint, scenario) =>
            {
                PersistenceExtensions<CosmosPersistence> persistence = endpoint.UsePersistence<CosmosPersistence>();
                persistence.Sagas().EnableMigrationMode();

                endpoint.LimitMessageProcessingConcurrencyTo(scenario.ConcurrentMessageCount);

                RecoverabilitySettings recoverability = endpoint.Recoverability();

                recoverability.Immediate(immediateRetries =>
                {
                    immediateRetries.OnMessageBeingRetried((m, _) =>
                    {
                        scenario.IncrementRetryCount();
                        return Task.CompletedTask;
                    });

                    immediateRetries.NumberOfRetries(scenario.ConcurrentMessageCount);
                });

                recoverability.Delayed(s => s.NumberOfRetries(0));
            });

        internal class HighContentionSaga(HighContentionScenario scenario) : Saga<HighContentionSaga.HighContentionSagaData>, IAmStartedByMessages<StartSaga>, IHandleMessages<ConcurrentMessage>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<HighContentionSagaData> mapper) =>
                mapper.MapSaga(saga => saga.SomeId)
                    .ToMessage<StartSaga>(msg => msg.SomeId)
                    .ToMessage<ConcurrentMessage>(msg => msg.SomeId);

            // will never be called
            public Task Handle(StartSaga message, IMessageHandlerContext context) => Task.CompletedTask;

            public class HighContentionSagaData : ContainSagaData
            {
                public Guid SomeId { get; set; }

                public int HitCount { get; set; }
            }

            public async Task Handle(ConcurrentMessage message, IMessageHandlerContext context)
            {
                Data.HitCount++;

                if (Data.HitCount >= scenario.ConcurrentMessageCount)
                {
                    MarkAsComplete();
                    await context.SendLocal(new SagaCompleted
                    {
                        SomeId = message.SomeId,
                        HitCount = Data.HitCount
                    });
                }
            }
        }

        class DoneHandler(HighContentionScenario scenario) : IHandleMessages<SagaCompleted>
        {
            public Task Handle(SagaCompleted message, IMessageHandlerContext context)
            {
                scenario.SagaCompleted = true;
                return Task.CompletedTask;
            }
        }
    }

    public class StartSaga : IMessage
    {
        public Guid SomeId { get; set; }
    }

    public class ConcurrentMessage : IMessage
    {
        public Guid SomeId { get; set; }
    }

    public class SagaCompleted : IMessage
    {
        public Guid SomeId { get; set; }

        public int HitCount { get; set; }
    }
}