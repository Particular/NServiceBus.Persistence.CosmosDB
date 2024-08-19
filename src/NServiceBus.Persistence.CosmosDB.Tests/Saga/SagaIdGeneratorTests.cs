namespace NServiceBus.Persistence.CosmosDB.Tests.Saga
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    public class SagaIdGeneratorTests
    {
        [Test]
        public void Should_return_the_same_guid_regardless_of_the_tfm() =>
            Assert.That(CosmosSagaIdGenerator.Generate("SagaEntityTypeFullName", "CorrelationPropertyName", "SomeValue"), Is.EqualTo(new Guid("c718ea15-a555-f79f-fa37-62477f3b07ca")));

        [TestCaseSource("RandomInput")]
        public void Should_return_the_same_guid_as_previous(string fullname, string propertyName, string propertyValue) =>
            Assert.That(CosmosSagaIdGenerator.Generate(fullname, propertyName, propertyValue), Is.EqualTo(PreviousCosmosSagaIdGenerator.Generate(fullname, propertyName, propertyValue)));

        public static IEnumerable<object[]> RandomInput
        {
            get
            {
                for (int i = 0; i < 10; i++)
                {
                    yield return new[]
                    {
                        TestContext.CurrentContext.Random.GetString(),
                        TestContext.CurrentContext.Random.GetString(),
                        TestContext.CurrentContext.Random.GetString()
                    };
                }
            }
        }

        static class PreviousCosmosSagaIdGenerator
        {
            public static Guid Generate(string sagaEntityTypeFullName, string correlationPropertyName, object correlationPropertyValue)
            {
                // assumes single correlated sagas since v6 doesn't allow more than one corr prop
                // will still have to use a GUID since moving to a string id will have to wait since its a breaking change
                var serializedPropertyValue = JsonConvert.SerializeObject(correlationPropertyValue);
                return DeterministicGuid($"{sagaEntityTypeFullName}_{correlationPropertyName}_{serializedPropertyValue}");
            }

            static Guid DeterministicGuid(string src)
            {
                var stringBytes = Encoding.UTF8.GetBytes(src);

                using var sha1CryptoServiceProvider = SHA1.Create();
                var hashedBytes = sha1CryptoServiceProvider.ComputeHash(stringBytes);
                Array.Resize(ref hashedBytes, 16);
                return new Guid(hashedBytes);
            }
        }
    }
}