namespace NServiceBus.AcceptanceTests;

using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Faults;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

//[TestFixture]
//public class When_no_partition_key_is_configured : NServiceBusAcceptanceTest
//{
//    [Test]
//    public async Task Should_throw_meaningful_exception()
//    {
//        var runSettings = new RunSettings();
//        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

//        Context context = await Scenario.Define<Context>()
//            .WithEndpoint<Endpoint>(b =>
//            {
//                b.DoNotFailOnErrorMessages();
//                b.When(s => s.SendLocal(new MyMessage()));
//            })
//            .Done(c => c.FailedMessages.Any())
//            .Run(runSettings);

//        FailedMessage failure = context.FailedMessages.FirstOrDefault()
//            .Value.First();

//        Assert.That(failure.Exception.Message, Does.Contain("partition key"));
//    }

//    class Context : ScenarioContext
//    {
//    }

//    class Endpoint : EndpointConfigurationBuilder
//    {
//        public Endpoint() =>
//            EndpointSetup<DefaultServer>((config, runDescriptor) =>
//            {
//                config.EnableOutbox();
//                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
//            });

//        class MyMessageHandler : IHandleMessages<MyMessage>
//        {
//            public Task Handle(MyMessage message, IMessageHandlerContext context)
//            {
//                Assert.Fail("Should not be called");
//                return Task.CompletedTask;
//            }
//        }
//    }

//    class MyMessage : IMessage
//    {
//    }
//}