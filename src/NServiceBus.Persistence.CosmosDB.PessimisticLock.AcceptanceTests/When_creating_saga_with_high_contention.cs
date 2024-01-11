namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    [TestFixture]
    public class When_creating_saga_with_high_contention : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_run_handler_only_once()
        {
            var scenario = await Scenario.Define<HighCreateContentionScenario>()
                .WithEndpoint<HighCreateContentionEndpoint>(behavior =>
                {
                    behavior.When(async session =>
                    {
                        var contentiousId = Guid.NewGuid();
                        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => session.SendLocal(new StartSaga { SomeId = contentiousId })));
                    });
                })
                .Done(s => s.SagaCompleted)
                .Run();

            Assert.IsTrue(scenario.ConcurrentMessagesSent);
            Assert.AreEqual((0, 1), (scenario.RetryCount, scenario.SagaStartCount));
        }

        public class HighCreateContentionScenario : ScenarioContext
        {
            long retryCount;

            long sagaStartCount;

            public int ConcurrentMessageCount { get; } = 20;

            public bool ConcurrentMessagesSent { get; set; }

            public bool SagaCompleted { get; set; }

            public long SagaStartCount => Interlocked.Read(ref sagaStartCount);

            public void IncrementSagaStartCount() => Interlocked.Increment(ref sagaStartCount);

            public long RetryCount => Interlocked.Read(ref retryCount);

            public void IncrementRetryCount() => Interlocked.Increment(ref retryCount);
        }

        class HighCreateContentionEndpoint : EndpointConfigurationBuilder
        {
            public HighCreateContentionEndpoint()
            {
                EndpointSetup<DefaultServer, HighCreateContentionScenario>((endpoint, scenario) =>
                {
                    endpoint.LimitMessageProcessingConcurrencyTo(scenario.ConcurrentMessageCount);

                    var recoverability = endpoint.Recoverability();

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
            }

            class HighContentionSaga : Saga<HighContentionSaga.HighContentionSagaData>, IAmStartedByMessages<StartSaga>, IHandleMessages<ConcurrentMessage>
            {
                readonly HighCreateContentionScenario scenario;

                public HighContentionSaga(HighCreateContentionScenario scenario) => this.scenario = scenario;

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<HighContentionSagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(message => message.SomeId).ToSaga(data => data.SomeId);
                    mapper.ConfigureMapping<ConcurrentMessage>(message => message.SomeId).ToSaga(data => data.SomeId);
                }

                public async Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    scenario.IncrementSagaStartCount();

                    await TestContext.Progress.WriteLineAsync($"Starting saga for: {message.SomeId}");
                    await Task.WhenAll(Enumerable.Range(0, scenario.ConcurrentMessageCount).Select(_ => context.SendLocal(new ConcurrentMessage { SomeId = message.SomeId })));
                    scenario.ConcurrentMessagesSent = true;
                }

                public class HighContentionSagaData : ContainSagaData
                {
                    public Guid SomeId { get; set; }

                    public int HitCount { get; set; }
                }

                public async Task Handle(ConcurrentMessage message, IMessageHandlerContext context)
                {
                    Data.HitCount++;
                    await TestContext.Progress.WriteLineAsync($"ConcurrentMessage for: {message.SomeId}");


                    if (Data.HitCount >= scenario.ConcurrentMessageCount)
                    {
                        MarkAsComplete();
                        await context.SendLocal(new SagaCompleted { SomeId = message.SomeId, HitCount = Data.HitCount });
                        await TestContext.Progress.WriteLineAsync($"ConcurrentMessage marked saga as completed for: {message.SomeId}");
                    }
                }
            }

            class DoneHandler : IHandleMessages<SagaCompleted>
            {
                readonly HighCreateContentionScenario scenario;

                public DoneHandler(HighCreateContentionScenario scenario) => this.scenario = scenario;

                public async Task Handle(SagaCompleted message, IMessageHandlerContext context)
                {
                    await TestContext.Progress.WriteLineAsync($"SagaCompleted for: {message.SomeId}");
                    scenario.SagaCompleted = true;
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
}