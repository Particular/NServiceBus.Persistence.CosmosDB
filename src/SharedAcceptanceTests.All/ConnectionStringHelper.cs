namespace NServiceBus.AcceptanceTests;

using System;

public static class ConnectionStringHelper
{
    public static string GetConnectionStringOrFallback(string environmentVariableName = "CosmosDBPersistence_ConnectionString", string fallbackEmulatorConnectionString = EmulatorConnectionString)
    {
        string candidate = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User);
        string environmentVariableConnectionString = string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(environmentVariableName) : candidate;

        return string.IsNullOrEmpty(environmentVariableConnectionString) ? fallbackEmulatorConnectionString : environmentVariableConnectionString;
    }

    public static bool IsRunningWithEmulator => GetConnectionStringOrFallback() == EmulatorConnectionString;

    const string EmulatorConnectionString = "AccountEndpoint = https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
}