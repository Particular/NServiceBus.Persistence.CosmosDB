namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Headers = Headers;

    public class When_custom_container_is_overwritten : NServiceBusAcceptanceTest
    {
        string nonDefaultContainerName;
        Container nonDefaultContainer;

        [SetUp]
        public async Task Setup()
        {
            nonDefaultContainerName = $"{SetupFixture.ContainerName}_shippingsagas";

            await SetupFixture.CosmosDbClient.CreateDatabaseIfNotExistsAsync(SetupFixture.DatabaseName)
                .ConfigureAwait(false);

            var database = SetupFixture.CosmosDbClient.GetDatabase(SetupFixture.DatabaseName);

            var containerProperties =
                new ContainerProperties(nonDefaultContainerName, SetupFixture.PartitionPathKey)
                {
                    // in order for individual items TTL to work (example outbox records)
                    DefaultTimeToLive = -1
                };

            await database.CreateContainerIfNotExistsAsync(containerProperties)
                .ConfigureAwait(false);

            nonDefaultContainer = database.GetContainer(nonDefaultContainerName);
        }

        [Test]
        public async Task Should_persist_into_the_provided_container()
        {
            var runSettings = new RunSettings();
            runSettings.DoNotRegisterDefaultContainerInformationProvider();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithFluentExtractor>(b => b.When((session, ctx) =>
                {
                    ctx.NonDefaultContainerName = nonDefaultContainerName;
                    return session.SendLocal(new StartOrder { OrderId = Guid.NewGuid() });
                }))
                .Done(c => c.OrderCompleted)
                .Run(runSettings);

            var orderProcessSagaData = await SetupFixture.Container.ReadItemAsync<dynamic>(context.OrderProcessId,
                new PartitionKey(context.OrderProcessId));
            var shippingProcessSagaData = await nonDefaultContainer.ReadItemAsync<dynamic>(context.ShippingProcessId,
                new PartitionKey(context.ShippingProcessId));

            Assert.Multiple(() =>
            {
                Assert.That(orderProcessSagaData.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(shippingProcessSagaData.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            });
        }

        [TearDown]
        public async Task Teardown() => await nonDefaultContainer.DeleteContainerStreamAsync();

        public class Context : ScenarioContext
        {
            public string NonDefaultContainerName { get; set; }
            public bool OrderCompleted { get; set; }
            public string OrderProcessId { get; set; }
            public string ShippingProcessId { get; set; }
        }

        public class EndpointWithFluentExtractor : EndpointConfigurationBuilder
        {
            public EndpointWithFluentExtractor()
            {
                EndpointSetup<DefaultServer>((config, r) =>
                {
                    var persistence = config.UsePersistence<CosmosPersistence>();
                    var transactionInformation = persistence.TransactionInformation();
                    transactionInformation.ExtractContainerInformationFromMessage<ShipOrder, Context>((_, ctx) =>
                        new ContainerInformation(ctx.NonDefaultContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)), (Context)r.ScenarioContext);
                    transactionInformation.ExtractContainerInformationFromHeaders((headers, ctx) =>
                    {
                        if (headers.TryGetValue(Headers.SagaType, out var sagaTypeHeader) && sagaTypeHeader.Contains(nameof(ShipOrderSaga)))
                        {
                            return new ContainerInformation(ctx.NonDefaultContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                        }

                        return null;
                    }, (Context)r.ScenarioContext);
                });
            }

            public class OrderCompletedHandler : IHandleMessages<OrderCompleted>
            {
                readonly Context testContext;

                public OrderCompletedHandler(Context testContext) => this.testContext = testContext;
                public Task Handle(OrderCompleted message, IMessageHandlerContext context)
                {
                    testContext.OrderProcessId = message.OrderProcessId.ToString();
                    testContext.ShippingProcessId = message.ShippingProcessId.ToString();
                    testContext.OrderCompleted = true;
                    return Task.CompletedTask;
                }
            }

            public class OrderSaga :
                Saga<OrderSagaData>,
                IAmStartedByMessages<StartOrder>,
                IHandleMessages<CompleteOrder>
            {
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderSagaData> mapper) =>
                    mapper.MapSaga(saga => saga.OrderId)
                        .ToMessage<StartOrder>(msg => msg.OrderId)
                        .ToMessage<CompleteOrder>(msg => msg.OrderId);

                public Task Handle(StartOrder message, IMessageHandlerContext context)
                {
                    Data.OrderId = message.OrderId;
                    Data.OrderDescription = $"The saga for order {message.OrderId}";

                    var shipOrder = new ShipOrder
                    {
                        OrderId = message.OrderId
                    };

                    return context.SendLocal(shipOrder);
                }

                public Task Handle(CompleteOrder message, IMessageHandlerContext context)
                {
                    var orderCompleted = new OrderCompleted
                    {
                        OrderId = Data.OrderId,
                        OrderProcessId = Data.Id,
                        ShippingProcessId = message.ShippingProcessId
                    };
                    return context.Publish(orderCompleted);
                }
            }

            public class OrderSagaData :
                ContainSagaData
            {
                public Guid OrderId { get; set; }
                public string OrderDescription { get; set; }
            }

            public class ShipOrderSaga :
                Saga<ShipOrderSagaData>,
                IAmStartedByMessages<ShipOrder>,
                IHandleTimeouts<CompleteOrder>
            {
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ShipOrderSagaData> mapper) =>
                    mapper.MapSaga(saga => saga.OrderId).ToMessage<ShipOrder>(msg => msg.OrderId);

                public Task Handle(ShipOrder message, IMessageHandlerContext context)
                {
                    Data.OrderId = message.OrderId;

                    var timeoutData = new CompleteOrder();
                    return RequestTimeout(context, TimeSpan.FromSeconds(1), timeoutData);
                }

                public Task Timeout(CompleteOrder state, IMessageHandlerContext context)
                {
                    state.OrderId = Data.OrderId;
                    state.ShippingProcessId = Data.Id;

                    return ReplyToOriginator(context, state);
                }
            }

            public class ShipOrderSagaData :
                ContainSagaData
            {
                public Guid OrderId { get; set; }
            }
        }

        public class StartOrder :
            IMessage
        {
            public Guid OrderId { get; set; }
        }

        public class OrderCompleted :
            IEvent
        {
            public Guid OrderId { get; set; }
            public Guid ShippingProcessId { get; set; }
            public Guid OrderProcessId { get; set; }
        }

        public class CompleteOrder
        {
            public Guid OrderId { get; set; }
            public Guid ShippingProcessId { get; set; }
        }

        public class ShipOrder :
            IMessage
        {
            public Guid OrderId { get; set; }
        }
    }
}