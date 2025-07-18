﻿namespace NServiceBus.AcceptanceTests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class When_storing_saga_with_high_contention : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_succeed_without_retries()
    {
        HighContentionScenario scenario = await Scenario.Define<HighContentionScenario>()
            .WithEndpoint<HighContentionEndpoint>(behavior =>
            {
                behavior.When(session => session.SendLocal(new StartSaga { SomeId = Guid.NewGuid() }));
            })
            .Done(s => s.SagaCompleted)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(scenario.ConcurrentMessagesSent, Is.True);
            Assert.That(scenario.RetryCount, Is.EqualTo(0));
        });
    }

    public class HighContentionScenario : ScenarioContext
    {
        long retryCount;

        public int ConcurrentMessageCount { get; } = 20;

        public bool ConcurrentMessagesSent { get; set; }

        public bool SagaCompleted { get; set; }

        public long RetryCount => Interlocked.Read(ref retryCount);

        public void IncrementRetryCount() => Interlocked.Increment(ref retryCount);
    }

    class HighContentionEndpoint : EndpointConfigurationBuilder
    {
        public HighContentionEndpoint() =>
            EndpointSetup<DefaultServer, HighContentionScenario>((endpoint, scenario) =>
            {
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

        class HighContentionSaga(HighContentionScenario scenario) : Saga<HighContentionSaga.HighContentionSagaData>, IAmStartedByMessages<StartSaga>, IHandleMessages<ConcurrentMessage>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<HighContentionSagaData> mapper)
            {
                mapper.ConfigureMapping<StartSaga>(message => message.SomeId).ToSaga(data => data.SomeId);
                mapper.ConfigureMapping<ConcurrentMessage>(message => message.SomeId).ToSaga(data => data.SomeId);
            }

            public async Task Handle(StartSaga message, IMessageHandlerContext context)
            {
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