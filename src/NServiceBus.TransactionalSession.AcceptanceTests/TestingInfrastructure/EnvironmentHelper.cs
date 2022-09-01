using System;

public static class EnvironmentHelper
{
    public static string GetEnvironmentVariable(string variable)
    {
        var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);

        return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
    }
}