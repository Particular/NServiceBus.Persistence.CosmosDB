namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class TransactionInformationExtractorTests
    {
        TransactionInformationExtractor extractor;
        IExtractTransactionInformationFromHeaders headerExtractor;
        ContainerInformation fakeContainerInformation;

        [SetUp]
        public void SetUp()
        {
            fakeContainerInformation = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));

            extractor = new TransactionInformationExtractor();

            headerExtractor = extractor;
        }

        [Test]
        public void Should_not_extract_from_header_with_no_matching_key()
        {
            extractor.ExtractFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.False);
            Assert.That(partitionKey, Is.Null);
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_from_header_with_first_match_winning()
        {
            extractor.ExtractFromHeader("HeaderKey");
            extractor.ExtractFromHeader("AnotherHeaderKey");

            var headers = new Dictionary<string, string>
            {
                { "AnotherHeaderKey", "AnotherHeaderValue" },
                { "HeaderKey", "HeaderValue" }
            };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key()
        {
            extractor.ExtractFromHeader("HeaderKey");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_extract_from_header_with_key_and_converter()
        {
            extractor.ExtractFromHeader("HeaderKey", value => value.Replace("__TOBEREMOVED__", string.Empty));

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", value => value.Replace("__TOBEREMOVED__", string.Empty), fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_and_state()
        {
            extractor.ExtractFromHeader("HeaderKey", (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__");

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Null);
        }

        [Test]
        public void Should_extract_from_header_with_key_converter_state_and_container_information()
        {
            extractor.ExtractFromHeader("HeaderKey", (value, toBeRemoved) => value.Replace(toBeRemoved, string.Empty), "__TOBEREMOVED__", fakeContainerInformation);

            var headers = new Dictionary<string, string> { { "HeaderKey", "HeaderValue__TOBEREMOVED__" } };

            var wasExtracted = headerExtractor.TryExtract(headers, out var partitionKey, out var containerInformation);

            Assert.That(wasExtracted, Is.True);
            Assert.That(partitionKey, Is.Not.Null.And.EqualTo(new PartitionKey("HeaderValue")));
            Assert.That(containerInformation, Is.Not.Null.And.EqualTo(fakeContainerInformation));
        }
    }
}