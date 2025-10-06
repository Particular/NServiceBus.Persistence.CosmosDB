namespace NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

[TestFixture]
public partial class When_default_container_and_extractor_is_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_not_overwrite_default_with_extractor_container()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint(new Endpoint(false), b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.That(context.Container.Id, Is.EqualTo(defaultContainerName));
    }
}