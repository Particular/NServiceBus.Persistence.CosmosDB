namespace NServiceBus.Persistence.CosmosDB.Tests.Saga
{

    using System;
    using NUnit.Framework;

    [TestFixture]
    public class SagaIdGeneratorTests
    {
        [Test]
        public void Should_deterministicly_return_the_same_guid_regardless_of_the_tfm() =>
            Assert.AreEqual(new Guid("c718ea15-a555-f79f-fa37-62477f3b07ca"), CosmosSagaIdGenerator.Generate("SagaEntityTypeFullName", "CorrelationPropertyName", "SomeValue"));
    }
}