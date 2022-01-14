namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class PartitionKeyExtractorTests
    {
        PartitionKeyExtractor extractor;

        [SetUp]
        public void SetUp()
        {
            extractor = new PartitionKeyExtractor();
        }

        [Test]
        public void Should_not_extract_from_header_with_no_matching_key()
        {
            extractor.ExtractPartitionKeyFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_first_match_winning()
        {
            extractor.ExtractPartitionKeyFromHeader("HeaderKey");
            extractor.ExtractPartitionKeyFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string>
            {
                { "AnotherHeaderKey", "AnotherHeaderValue" },
                { "HeaderKey", "HeaderValue" }
            };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
        }

        [Test]
        public void Should_extract_from_header_with_key()
        {
            extractor.ExtractPartitionKeyFromHeader("HeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter()
        {
            extractor.ExtractPartitionKeyFromHeader("HeaderKey", value => value.Replace("__TOBEREMOVED__", string.Empty));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_and_argument()
        {
            extractor.ExtractPartitionKeyFromHeader("HeaderKey", (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = extractor.TryExtract(headers, out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
        }

        [Test]
        public void Should_throw_when_header_is_already_mapped()
        {
            extractor.ExtractPartitionKeyFromHeader("HeaderKey");

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractPartitionKeyFromHeader("HeaderKey"));

            Assert.That(exception.Message, Contains.Substring("The header key 'HeaderKey' is already being handled by a header extractor and cannot be processed by another one."));
        }

        [Test]
        public void Should_not_extract_from_message_with_no_match()
        {
            extractor.ExtractPartitionKeyFromMessage<MyMessage>(m => new PartitionKey(m.SomeId.ToString()));
            extractor.ExtractPartitionKeyFromMessage<MyOtherMessage>(m => new PartitionKey(m.SomeId.ToString()));

            var message = new MyUnrelatedMessage();

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
        }

        [Test]
        public void Should_extract_from_message_with_first_match_winning()
        {
            extractor.ExtractPartitionKeyFromMessage<IProvideSomeId>(m => new PartitionKey(m.SomeId));
            extractor.ExtractPartitionKeyFromMessage<MyMessageWithInterfaces>(m => new PartitionKey(string.Join(";", Enumerable.Repeat(m.SomeId, 2))));

            var message = new MyMessageWithInterfaces { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
        }

        [Test]
        public void Should_extract_from_message()
        {
            extractor.ExtractPartitionKeyFromMessage<MyMessage>(m => new PartitionKey(m.SomeId));

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SomeValue")));
        }

        [Test]
        public void Should_extract_from_message_with_argument()
        {
            extractor.ExtractPartitionKeyFromMessage<MyMessage, ArgumentHelper>((m, helper) => new PartitionKey(helper.Upper(m.SomeId)), new ArgumentHelper());

            var message = new MyMessage { SomeId = "SomeValue" };

            var wasExtracted = extractor.TryExtract(message, new Dictionary<string, string>(), out var partitionKey);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("SOMEVALUE")));
        }

        [Test]
        public void Should_throw_when_message_type_is_already_mapped()
        {
            extractor.ExtractPartitionKeyFromMessage<MyMessage>(m => new PartitionKey(m.SomeId));

            var exception = Assert.Throws<ArgumentException>(() => extractor.ExtractPartitionKeyFromMessage<MyMessage>(m => new PartitionKey(m.SomeId)));

            Assert.That(exception.Message, Contains.Substring("The message type 'NServiceBus.Persistence.CosmosDB.Tests.Transaction.PartitionKeyExtractorTests+MyMessage' is already being handled by a message extractor and cannot be processed by another one. "));
        }

        // TODO: Add more tests that verify header passing

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