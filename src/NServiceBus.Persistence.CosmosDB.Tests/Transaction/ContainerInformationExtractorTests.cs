namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        public void Should_extract_from_header_with_key_and_extractor()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey",
                value => new ContainerInformation(value.Replace("__TOBEREMOVED__", string.Empty), fakePartitionKeyPath));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_extractor_and_extractor_argument()
        {
            extractor.ExtractContainerInformationFromHeader("HeaderKey",
                (value, toBeRemoved) => new ContainerInformation(value.Replace(toBeRemoved, string.Empty), fakePartitionKeyPath), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(new ContainerInformation("HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_headers_with_extractor()
        {
            extractor.ExtractContainerInformationFromHeaders(
                hdrs => new ContainerInformation(hdrs["HeaderKey"], fakePartitionKeyPath));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

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

            Assert.That(exception.Message, Contains.Substring("The header key 'HeaderKey' is already being handled by a container header extractor and cannot be processed by another one."));
        }

        [Test]
        public void Should_not_extract_from_message_with_no_match()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath));
            extractor.ExtractContainerInformationFromMessage<MyOtherMessage>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath));

            var message = new MyUnrelatedMessage();

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var containerInformation);

            Assert.That(wasExtracted, Is.False);
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_message_with_first_match_winning()
        {
            extractor.ExtractContainerInformationFromMessage<IProvideSomeId>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath));
            extractor.ExtractContainerInformationFromMessage<MyMessageWithInterfaces>(m => new ContainerInformation(string.Join(";", Enumerable.Repeat(m.SomeId, 2)), fakePartitionKeyPath));

            var message = new MyMessageWithInterfaces { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("SomeValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_message()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath));

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("SomeValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_message_with_argument()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage, ArgumentHelper>((m, helper) => new ContainerInformation(helper.Upper(m.SomeId), fakePartitionKeyPath), new ArgumentHelper());

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("SOMEVALUE", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_message_with_headers()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage, ArgumentHelper>((m, hdrs, helper) =>
                new ContainerInformation($"{helper.Upper(m.SomeId)}_{hdrs["HeaderKey"]}", fakePartitionKeyPath), new ArgumentHelper());

            var message = new MyMessage { SomeId = "SomeValue" };
            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(message, headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("SOMEVALUE_HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_extract_from_message_with_headers_and_argument()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage>((m, hdrs) =>
                new ContainerInformation($"{m.SomeId}_{hdrs["HeaderKey"]}", fakePartitionKeyPath));

            var message = new MyMessage { SomeId = "SomeValue" };
            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(message, headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new ContainerInformation("SomeValue_HeaderValue", fakePartitionKeyPath)));
        }

        [Test]
        public void Should_throw_when_message_type_is_already_mapped()
        {
            extractor.ExtractContainerInformationFromMessage<MyMessage>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath));

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractContainerInformationFromMessage<MyMessage>(m => new ContainerInformation(m.SomeId.ToString(), fakePartitionKeyPath)));

            Assert.That(exception.Message, Contains.Substring("The message type 'NServiceBus.Persistence.CosmosDB.Tests.Transaction.ContainerInformationExtractorTests+MyMessage' is already being handled by a container message extractor and cannot be processed by another one."));
        }

        class MyMessage
        {
            public string SomeId { get; set; }
        }

        class MyOtherMessage
        {
            public string SomeId { get; set; }
        }

        class MyUnrelatedMessage
        {
            public string SomeId { get; set; }
        }

        class MyMessageWithInterfaces : IProvideSomeId
        {
            public string SomeId { get; set; }
        }
        interface IProvideSomeId
        {
            string SomeId { get; set; }
        }

        // Just a silly helper to show that state can be passed
        class ArgumentHelper
        {
            public string Upper(string input) => input.ToUpperInvariant();
        }
    }
}