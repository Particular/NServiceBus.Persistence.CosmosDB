namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class TransactionInformationExtractorTests
    {
        TransactionInformationExtractor extractor;
        ContainerInformation fakeContainerInformation;

        [SetUp]
        public void SetUp()
        {
            fakeContainerInformation = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));

            extractor = new TransactionInformationExtractor();
        }

        [Test]
        public void Should_not_extract_from_header_with_no_matching_key()
        {
            extractor.ExtractFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_first_match_winning()
        {
            extractor.ExtractFromHeader("HeaderKey");
            extractor.ExtractFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string>
            {
                { "AnotherHeaderKey", "AnotherHeaderValue" },
                { "HeaderKey", "HeaderValue" }
            };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key()
        {
            extractor.ExtractFromHeader("HeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter()
        {
            extractor.ExtractFromHeader("HeaderKey", value => value.Replace("__TOBEREMOVED__", string.Empty));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", value => value.Replace("__TOBEREMOVED__", string.Empty), fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_and_argument()
        {
            extractor.ExtractFromHeader("HeaderKey", (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_argument_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__", fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_throw_when_header_is_already_mapped()
        {
            extractor.ExtractFromHeader("HeaderKey");

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractFromHeader("HeaderKey"));

            Assert.That(exception.Message, Contains.Substring("The header key 'HeaderKey' is already being handled by a header extractor and cannot be processed by another one."));
        }

        [Test]
        public void Should_not_extract_from_message_with_no_match()
        {
            extractor.ExtractFromMessage<MyMessage>(m => new PartitionKey(m.SomeId.ToString()));
            extractor.ExtractFromMessage<MyOtherMessage>(m => new PartitionKey(m.SomeId.ToString()));

            var message = new MyUnrelatedMessage();

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_message_with_first_match_winning()
        {
            extractor.ExtractFromMessage<IProvideSomeId>(m => new PartitionKey(m.SomeId));
            extractor.ExtractFromMessage<MyMessageWithInterfaces>(m => new PartitionKey(string.Join(";", Enumerable.Repeat(m.SomeId, 2))));

            var message = new MyMessageWithInterfaces { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_message()
        {
            extractor.ExtractFromMessage<MyMessage>(m => new PartitionKey(m.SomeId));

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_message_with_container_information()
        {
            extractor.ExtractFromMessage<MyMessage>(m => new PartitionKey(m.SomeId), fakeContainerInformation);

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_extract_from_message_with_argument()
        {
            extractor.ExtractFromMessage<MyMessage, ArgumentHelper>((m, helper) => new PartitionKey(helper.Upper(m.SomeId)), new ArgumentHelper());

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SOMEVALUE")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_message_with_argument_and_container_information()
        {
            extractor.ExtractFromMessage<MyMessage, ArgumentHelper>((m, helper) => new PartitionKey(helper.Upper(m.SomeId)), new ArgumentHelper(), fakeContainerInformation);

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SOMEVALUE")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_throw_when_message_type_is_already_mapped()
        {
            extractor.ExtractFromMessage<MyMessage>(m => new PartitionKey(m.SomeId));

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractFromMessage<MyMessage>(m => new PartitionKey(m.SomeId)));

            Assert.That(exception.Message, Contains.Substring("The message type 'NServiceBus.Persistence.CosmosDB.Tests.Transaction.TransactionInformationExtractorTests+MyMessage' is already being handled by a message extractor and cannot be processed by another one."));
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