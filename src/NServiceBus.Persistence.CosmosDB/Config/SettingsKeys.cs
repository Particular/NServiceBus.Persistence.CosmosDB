namespace NServiceBus.Persistence.CosmosDB
{
    static class SettingsKeys
    {
        const string baseName = "CosmosDB.";
        public const string DatabaseName = nameof(baseName) + nameof(DatabaseName);
        public const string OutboxTimeToLiveInSeconds = nameof(baseName) + nameof(OutboxTimeToLiveInSeconds);
        public const string EnableMigrationMode = nameof(baseName) + nameof(EnableMigrationMode);
        public const string EnablePessimisticsLocking = nameof(baseName) + nameof(EnablePessimisticsLocking);
        public const string LeaseLockTime = nameof(baseName) + nameof(LeaseLockTime);
        public const string LeaseLockAcquisitionMaximumRefreshDelay = nameof(baseName) + nameof(LeaseLockAcquisitionMaximumRefreshDelay);
        public const string LeaseLockAcquisitionTimeout = nameof(baseName) + nameof(LeaseLockAcquisitionTimeout);
    }
}