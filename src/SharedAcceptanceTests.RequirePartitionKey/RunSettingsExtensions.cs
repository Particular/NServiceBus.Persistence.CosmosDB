namespace NServiceBus.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting.Support;

    public static class RunSettingsExtensions
    {
        public static void DoNotRegisterDefaultPartitionKeyProvider(this RunSettings runSettings) =>
            runSettings.Set(new DoNotRegisterDefaultPartitionKeyProvider());
    }
}