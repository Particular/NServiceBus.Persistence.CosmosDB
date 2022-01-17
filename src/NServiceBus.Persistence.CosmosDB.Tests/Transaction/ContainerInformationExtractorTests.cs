namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class ContainerInformationExtractorTests
    {
        PartitionKeyPath fakePartitionKeyPath;
        ContainerInformationExtractor extractor;

        [SetUp]
        public void SetUp()
        {
            fakePartitionKeyPath = new PartitionKeyPath("/deep/down");

            extractor = new ContainerInformationExtractor();
        }

        [Test]
        public void Should_not_extract_from_header_with_no_matching_key()
        {
            extractor.ExtractContainerInformationFromHeader("AnotherHeaderKey", value => new ContainerInformation());

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_first_match_winning()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey", value => new ContainerInformation(value, fakePartitionKeyPath));
            extractor.ExtractContainerInformationFromHeader("AnotherHeaderKey", value => new ContainerInformation(value, fakePartitionKeyPath));

            var headers = new Dictionary<string, string>
            {
                { "AnotherHeaderKey", "AnotherHeaderValue" },
                { "HeaderKey", "HeaderValue" }
            };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey",
                value => new ContainerInformation(value.Replace("__TOBEREMOVED__", string.Empty), fakePartitionKeyPath));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter_and_extractor_argument()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey",
                (value, toBeRemoved) => new ContainerInformation(value.Replace(toBeRemoved, string.Empty), fakePartitionKeyPath), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_headers_with_extractor_and_extractor_argument()
        {
            extractor.ExtractContainerInformationFromHeaders(
                (hdrs, toBeRemoved) => new ContainerInformation(hdrs["HeaderKey"].Replace(toBeRemoved, string.Empty), fakePartitionKeyPath), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_headers_with_custom_implementation()
        {
            extractor.ExtractContainerInformationFromHeaders(new CustomHeadersExtractor(fakePartitionKeyPath));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        class CustomHeadersExtractor : IContainerInformationFromHeadersExtractor
        {
            readonly PartitionKeyPath partitionKeyPath;

            public CustomHeadersExtractor(PartitionKeyPath partitionKeyPath) => this.partitionKeyPath = partitionKeyPath;

            public bool TryExtract(IReadOnlyDictionary<string, string> headers,
                out ContainerInformation? containerInformation)
            {
                containerInformation = new ContainerInformation(headers["HeaderKey"], partitionKeyPath);
                return true;
            }
        }

        [Test]
        public void Should_throw_when_header_is_already_mapped()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey", value => new ContainerInformation());

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractContainerInformationFromHeader("HeaderKey", value => new ContainerInformation()));

            Assert.That(exception.Message, Contains.Substring("The header key 'HeaderKey' is already being handled by a container extractor and cannot be processed by another one."));
        }

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