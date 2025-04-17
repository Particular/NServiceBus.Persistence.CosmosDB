namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class TransactionInformationConfigurationTests
{
    [Test]
    public void Should_have_all_relevant_extraction_apis_exposed()
    {
        IEnumerable<string> transactionInformationConfigurationMethods = typeof(TransactionInformationConfiguration)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Extract"))
            .OrderBy(m => m.Name)
            .ThenBy(m => m.GetParameters().Length)
            .Select(m => m.ToString());

        IEnumerable<string> containerInformationExtractorMethods = typeof(ContainerInformationExtractor)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Extract"))
            .OrderBy(m => m.Name)
            .ThenBy(m => m.GetParameters().Length)
            .Select(m => m.ToString());

        IEnumerable<string> partitionKeyExtractorMethods = typeof(PartitionKeyExtractor)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Extract"))
            .OrderBy(m => m.Name)
            .ThenBy(m => m.GetParameters().Length)
            .Select(m => m.ToString());

        // represents the missing extraction methods in the TransactionInformationConfiguration object
        string[] methodInfos = containerInformationExtractorMethods.Union(partitionKeyExtractorMethods).Except(transactionInformationConfigurationMethods)
            .Distinct()
            .ToArray();

        Approver.Verify(methodInfos.Length == 0 ? "Represents the missing extraction methods in the TransactionInformationConfiguration object and should remain empty" : string.Join(Environment.NewLine, methodInfos));
    }
}