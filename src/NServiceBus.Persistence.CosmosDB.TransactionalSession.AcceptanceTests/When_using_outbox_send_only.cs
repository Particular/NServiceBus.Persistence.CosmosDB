namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.CosmosDB;

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
            .WithServices(services =>
            {
                services.AddSingleton<IPartitionKeyFromHeadersExtractor>(sp =>
                    new PartitionKeyProvider(sp.GetRequiredService<Context>()));
            })
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
    }

    class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() => EndpointSetup<DefaultServer>(c =>
        {
            var persistence = c.GetSettings().Get<PersistenceExtensions<CosmosPersistence>>();

            var options = new TransactionalSessionOptions { ProcessorEndpoint = Conventions.EndpointNamingConvention.Invoke(typeof(ProcessorEndpoint)) };

            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
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

    class ProcessorEndpoint : EndpointConfigurationBuilder
    {
        public ProcessorEndpoint() => EndpointSetup<TransactionSessionWithOutboxEndpoint>();
    }

    class SampleMessage : ICommand
    {
    }

    class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            return true;
        }
    }
}