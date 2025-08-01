﻿namespace NServiceBus.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using Pipeline;
using NUnit.Framework;

public class When_regular_handler_with_no_container_information : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_work()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithRegularHandler>(b => b.When(session => session.SendLocal(new MyMessage())))
            .Done(c => c.MessageReceived)
            .Run(runSettings);

        Assert.That(context.MessageReceived, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class EndpointWithRegularHandler : EndpointConfigurationBuilder
    {
        public EndpointWithRegularHandler() =>
            EndpointSetup<DefaultServer>(config =>
            {
                config.Pipeline.Register(new ContainerInformationRemoverBehavior.Registration());
            });

        class ContainerInformationRemoverBehavior : Behavior<IIncomingLogicalMessageContext>
        {
            public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
            {
                context.Extensions.Remove<ContainerInformation>();
                return next();
            }

            public class Registration : RegisterStep
            {
                public Registration() : base(nameof(ContainerInformationRemoverBehavior),
                    typeof(ContainerInformationRemoverBehavior),
                    "Removes the container information if present",
                    b => new ContainerInformationRemoverBehavior()) =>
                    InsertBeforeIfExists("LogicalOutboxBehavior");
            }
        }

        public class AHandler : IHandleMessages<MyMessage>
        {
            public AHandler(Context testContext) => this.testContext = testContext;

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                return Task.CompletedTask;
            }

            readonly Context testContext;
        }
    }

    public class MyMessage : ICommand
    {
    }
}