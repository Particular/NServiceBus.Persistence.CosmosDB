namespace NServiceBus.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NServiceBus.AcceptanceTesting.Support;
using Features;
using Pipeline;
using Routing;
using Transport;
using NServiceBus.Unicast.Transport;
using NUnit.Framework;

[TestFixture]
public class When_using_outbox_control_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_work_with_no_extractors()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>()
            .Done(c => c.ProcessedControlMessage)
            .Run(runSettings)
            .ConfigureAwait(false);

        Assert.That(context.ProcessedControlMessage, Is.True);
    }

    [Test]
    public async Task Should_work_with_faulty_extractor()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();
        runSettings.RegisterFaultyPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>()
            .Done(c => c.ProcessedControlMessage)
            .Run(runSettings)
            .ConfigureAwait(false);

        Assert.That(context.ProcessedControlMessage, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool ProcessedControlMessage { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                config.RegisterStartupTask<ControlMessageSender>();
                config.Pipeline.Register(new ControlMessageBehavior(runDescriptor.ScenarioContext as Context), "Checks that the control message was processed successfully");
            });

        class ControlMessageSender(IMessageDispatcher dispatcher) : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                OutgoingMessage controlMessage = ControlMessageFactory.Create(MessageIntent.Subscribe);
                var messageOperation = new TransportOperation(controlMessage, new UnicastAddressTag(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint))));

                return dispatcher.Dispatch(new TransportOperations(messageOperation), new TransportTransaction(), cancellationToken);
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        class ControlMessageBehavior(Context testContext) : Behavior<IIncomingPhysicalMessageContext>
        {
            public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                await next();

                testContext.ProcessedControlMessage = true;
            }
        }
    }
}