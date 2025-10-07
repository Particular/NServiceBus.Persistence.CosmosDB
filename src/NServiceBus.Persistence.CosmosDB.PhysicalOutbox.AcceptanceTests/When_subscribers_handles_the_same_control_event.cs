namespace NServiceBus.AcceptanceTests.Outbox;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTests;
using EndpointTemplates;
using Features;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Pipeline;
using NUnit.Framework;

public class When_subscribers_handles_the_same_control_event : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_be_processed_by_all_subscribers_using_default_synthetic_key()
    {
        Requires.OutboxPersistence();

        var runSettings = new RunSettings
        {
            TestExecutionTimeout = TimeSpan.FromSeconds(30)
        };
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        var publishOptions = new PublishOptions();
        publishOptions.SetHeader(Headers.ControlMessageHeader, "true");

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b =>
                b.When(c => c.Subscriber1Subscribed && c.Subscriber2Subscribed, session => session.Publish(new MyEvent(), publishOptions))
            )
            .WithEndpoint<Subscriber1>(b => b.When(async (session, ctx) =>
            {
                await session.Subscribe<MyEvent>();
                if (ctx.HasNativePubSubSupport)
                {
                    ctx.Subscriber1Subscribed = true;
                    ctx.AddTrace("Subscriber1 is now subscribed (at least we have asked the broker to be subscribed)");
                }
                else
                {
                    ctx.AddTrace("Subscriber1 has now asked to be subscribed to MyEvent");
                }
            }))
            .WithEndpoint<Subscriber2>(b => b.When(async (session, ctx) =>
            {
                await session.Subscribe<MyEvent>();
                if (ctx.HasNativePubSubSupport)
                {
                    ctx.Subscriber2Subscribed = true;
                    ctx.AddTrace("Subscriber2 is now subscribed (at least we have asked the broker to be subscribed)");
                }
                else
                {
                    ctx.AddTrace("Subscriber2 has now asked to be subscribed to MyEvent");
                }
            }))
            .Done(c => c.Subscriber1GotTheEvent && c.Subscriber2GotTheEvent)
            .Run(runSettings);

        Assert.Multiple(() =>
        {
            Assert.That(context.Subscriber1GotTheEvent, Is.True);
            Assert.That(context.Subscriber2GotTheEvent, Is.True);
        });
    }

    public class Context : ScenarioContext
    {
        public bool Subscriber1Subscribed { get; set; }
        public bool Subscriber2Subscribed { get; set; }

        public bool Subscriber1GotTheEvent { get; set; }
        public bool Subscriber2GotTheEvent { get; set; }

        public bool ProcessedControlMessage { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() => EndpointSetup<DefaultPublisher>(c =>
        {
            c.OnEndpointSubscribed<Context>((s, context) =>
            {
                var subscriber1 = Conventions.EndpointNamingConvention(typeof(Subscriber1));
                if (s.SubscriberEndpoint.Contains(subscriber1))
                {
                    context.Subscriber1Subscribed = true;
                    context.AddTrace($"{subscriber1} is now subscribed");
                }

                var subscriber2 = Conventions.EndpointNamingConvention(typeof(Subscriber2));
                if (s.SubscriberEndpoint.Contains(subscriber2))
                {
                    context.Subscriber2Subscribed = true;
                    context.AddTrace($"{subscriber2} is now subscribed");
                }
            });
        }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(DefaultPublisher)));
    }

    public class Subscriber1 : EndpointConfigurationBuilder
    {
        public Subscriber1() =>
            EndpointSetup<DefaultServer>((c, runDescriptor) =>
            {
                c.DisableFeature<AutoSubscribe>();

                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                c.EnableOutbox();
                c.Pipeline.Register(new ControlMessageBehavior(runDescriptor.ScenarioContext as Context), "Checks that the control message was processed successfully");
            }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));

        class ControlMessageBehavior(Context testContext) : Behavior<IIncomingPhysicalMessageContext>
        {
            public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                await next();

                testContext.Subscriber1GotTheEvent = true;
            }
        }
    }

    public class Subscriber2 : EndpointConfigurationBuilder
    {
        public Subscriber2() =>
            EndpointSetup<DefaultServer>((c, runDescriptor) =>
            {
                c.DisableFeature<AutoSubscribe>();

                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                c.EnableOutbox();
                c.Pipeline.Register(new ControlMessageBehavior(runDescriptor.ScenarioContext as Context), "Checks that the control message was processed successfully");
            }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));

        class ControlMessageBehavior(Context testContext) : Behavior<IIncomingPhysicalMessageContext>
        {
            public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                await next();

                testContext.Subscriber2GotTheEvent = true;
            }
        }
    }

    public class MyEvent : IEvent;
}