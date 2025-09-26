namespace NServiceBus.AcceptanceTests;

using NServiceBus.AcceptanceTesting.Support;

public static partial class RunSettingsExtensions
{
    public static void DoNotRegisterDefaultPartitionKeyProvider(this RunSettings runSettings) =>
        runSettings.Set(new DoNotRegisterDefaultPartitionKeyProvider());

    public static void RegisterFaultyPartitionKeyProvider(this RunSettings runSettings) =>
        runSettings.Set(new RegisterFaultyPartitionKeyProvider());
}