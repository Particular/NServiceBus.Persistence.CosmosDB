namespace NServiceBus.AcceptanceTests;

using NServiceBus.AcceptanceTesting.Support;

public static partial class RunSettingsExtensions
{
    public static void DoNotRegisterDefaultContainerInformationProvider(this RunSettings runSettings) =>
        runSettings.Set(new DoNotRegisterDefaultContainerInformationProvider());

    public static void RegisterFaultyContainerInformationProvider(this RunSettings runSettings) =>
       runSettings.Set(new RegisterFaultyContainerProvider());
}