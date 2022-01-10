namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    /// <summary>
    /// The saga persistence configuration options.
    /// </summary>
    public class SagaPersistenceConfiguration
    {
        /// <summary>
        /// Enable support for sagas migrated from other persistence technologies by querying the saga from storage using a migrated saga id.
        /// </summary>
        public void EnableMigrationMode() => MigrationModeEnabled = true;

        /// <summary>
        /// Enables default saga persistence pessimistic locking. Default to optimistic locking when not used.
        /// </summary>
        public void UsePessimisticLocking() => PessimisticLockingEnabled = true;

        /// <summary>
        /// Set saga persistence pessimistic lease lock duration. Default is 60 seconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock duration.</param>
        public void SetPessimisticLeaseLockTime(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock time must be greater than zero.");
            }

            LeaseLockTime = value;
        }

        /// <summary>
        /// Set saga persistence pessimistic lease lock acquisition timeout. Default is 60 seconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock acquisition duration.</param>
        public void SetPessimisticLeaseLockAcquisitionTimeout(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition timeout must be greater than zero.");
            }

            LeaseLockAcquisitionTimeout = value;
        }

        /// <summary>
        /// Set maximum saga persistence lease lock acquisition refresh delay. Default is 20 milliseconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock acquisition maximum refresh duration.</param>
        public void SetPessimisticLeaseLockAcquisitionMaximumRefreshDelay(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition maximum refresh delay must be between zero and 1 second");
            }

            if (value > TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition maximum refresh delay must be between zero and 1 second");
            }

            if (value < LeaseLockAcquisitionMinimumRefreshDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Lease lock acquisition maximum refresh delay must be equal or larger than the minimum refresh delay ('{LeaseLockAcquisitionMinimumRefreshDelay}').");
            }

            LeaseLockAcquisitionMaximumRefreshDelay = value;
        }

        /// <summary>
        /// Set minimum saga persistence lease lock acquisition refresh delay. Default is 5 milliseconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock acquisition minimum refresh duration.</param>
        public void SetPessimisticLeaseLockAcquisitionMinimumRefreshDelay(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition minimum refresh delay must be between zero and 1 second");
            }

            if (value > TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition minimum refresh delay must be between zero and 1 second");
            }

            if (value > LeaseLockAcquisitionMaximumRefreshDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Lease lock acquisition minimum refresh delay must be equal or smaller than the maximum refresh delay ('{LeaseLockAcquisitionMaximumRefreshDelay}').");
            }

            LeaseLockAcquisitionMinimumRefreshDelay = value;
        }

        internal TimeSpan LeaseLockAcquisitionTimeout { get; private set; } = TimeSpan.FromSeconds(60);
        internal TimeSpan LeaseLockTime { get; private set; } = TimeSpan.FromSeconds(60);
        internal TimeSpan LeaseLockAcquisitionMaximumRefreshDelay { get; private set; } = TimeSpan.FromMilliseconds(20);
        internal TimeSpan LeaseLockAcquisitionMinimumRefreshDelay { get; private set; } = TimeSpan.FromMilliseconds(5);
        internal bool PessimisticLockingEnabled { get; private set; }
        internal bool MigrationModeEnabled { get; private set; }
    }
}