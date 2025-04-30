namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Pipeline;

public class When_using_outbox_send_only : NServiceBusAcceptanceTest
{
    [Test()]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SendOnlyEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                var options = new SendOptions();

                options.SetDestination(Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)));

                await transactionalSession.Send(new SampleMessage(), options);

                await transactionalSession.Commit(CancellationToken.None);
            }))
            .WithEndpoint<AnotherEndpoint>()
            .WithEndpoint<ProcessorEndpoint>()
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.ControlMessageReceived, Is.True);
        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public bool ControlMessageReceived { get; set; }
    }

    class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() => EndpointSetup<DefaultServer>((c, runDescriptor) =>
        {
            var persistence = c.GetSettings().Get<PersistenceExtensions<CosmosPersistence>>();

            var options = new TransactionalSessionOptions { ProcessorAddress = Conventions.EndpointNamingConvention.Invoke(typeof(ProcessorEndpoint)) };

            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder, IDoNotCaptureServiceProvider
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServer>();

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    class ProcessorEndpoint : EndpointConfigurationBuilder, IDoNotCaptureServiceProvider
    {
        public ProcessorEndpoint() => EndpointSetup<TransactionSessionDefaultServer>((c, runDescriptor) =>
            {
                c.Pipeline.Register(typeof(DiscoverControlMessagesBehavior), "Discovers control messages");
                c.EnableOutbox();
                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            }
        );

        class DiscoverControlMessagesBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.ContainsKey("todo"))
                {
                    testContext.ControlMessageReceived = true;
                }

                await next();
            }
        }
    }

    class SampleMessage : ICommand
    {
    }
}