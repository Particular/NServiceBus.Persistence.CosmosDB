namespace NServiceBus.Persistence.CosmosDB
{

    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class ContainerInformationExtractorTests
    {
        ContainerInformationExtractor extractor;
        ContainerInformation fakeContainerInformation;

        [SetUp]
        public void SetUp()
        {
            fakeContainerInformation = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));

            extractor = new ContainerInformationExtractor();
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter()
        {
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter_and_converter_argument()
        {
        }

        [Test]
        public void Should_extract_from_headers_with_extractor_and_extractor_argument()
        { }

        [Test]
        public void Should_extract_from_headers_with_custom_implementation()
        { }

        [Test]
        public void Should_extract_from_message_with_extractor()
        { }

        [Test]
        public void Should_extract_from_message_with_extractor_and_extractor_argument()
        { }

        [Test]
        public void Should_extract_from_message_and_headers_with_extractor()
        { }

        [Test]
        public void Should_extract_from_message_and_headers_with_extractor_and_extractor_argument()
        { }

        [Test]
        public void Should_extract_from_message_with_custom_implementation()
        { }

        //[Test]
        //public void Should_extract_from_header_with_key_converter_argument_and_container_information()
        //{
        //    extractor.ExtractPartitionKeyFromHeader("HeaderKey",
        //        (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__");

        //    var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

        //    var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

        //    Assert.That(wasExtracted, Is.True);
        //    Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
        //    Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        //}

        //[Test]
        //public void Should_extract_from_message_with_container_information()
        //{
        //    extractor.ExtractPartitionKeyFromMessage<MyMessage>(m => new PartitionKey(m.SomeId));

        //    var message = new MyMessage { SomeId = "SomeValue" };

        //    var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

        //    Assert.That(wasExtracted, Is.True);
        //    Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
        //    Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        //}

        //[Test]
        //public void Should_extract_from_message_with_argument_and_container_information()
        //{
        //    extractor.ExtractPartitionKeyFromMessage<MyMessage, ArgumentHelper>(
        //        (m, helper) => new PartitionKey(helper.Upper(m.SomeId)), new ArgumentHelper());

        //    var message = new MyMessage { SomeId = "SomeValue" };

        //    var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

        //    Assert.That(wasExtracted, Is.True);
        //    Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SOMEVALUE")));
        //    Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        //}
    }
}