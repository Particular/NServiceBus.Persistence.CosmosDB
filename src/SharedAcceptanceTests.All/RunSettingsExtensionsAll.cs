namespace NServiceBus.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting.Support;

    public static partial class RunSettingsExtensions
    {
        public static void DoNotRegisterDefaultContainerInformationProvider(this RunSettings runSettings) =>
            runSettings.Set(new DoNotRegisterDefaultContainerInformationProvider());
    }
}