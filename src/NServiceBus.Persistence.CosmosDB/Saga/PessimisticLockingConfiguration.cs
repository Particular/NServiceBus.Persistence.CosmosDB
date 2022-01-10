namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    /// <summary>
    /// The pessimistic locking configuration options
    /// </summary>
    public class PessimisticLockingConfiguration
    {
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
        /// Set maximum saga persistence lease lock acquisition refresh delay. Default is 1000 milliseconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock acquisition maximum refresh duration.</param>
        public void SetPessimisticLeaseLockAcquisitionMaximumRefreshDelay(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition maximum refresh delay must be equal or larger than zero.");
            }

            if (value < LeaseLockAcquisitionMinimumRefreshDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Lease lock acquisition maximum refresh delay must be equal or larger than the minimum refresh delay ('{LeaseLockAcquisitionMinimumRefreshDelay}').");
            }

            LeaseLockAcquisitionMaximumRefreshDelay = value;
        }

        /// <summary>
        /// Set minimum saga persistence lease lock acquisition refresh delay. Default is 500 milliseconds.
        /// </summary>
        /// <param name="value">Pessimistic lease lock acquisition minimum refresh duration.</param>
        public void SetPessimisticLeaseLockAcquisitionMinimumRefreshDelay(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Lease lock acquisition minimum refresh delay must be equal or larger than zero.");
            }

            if (value > LeaseLockAcquisitionMaximumRefreshDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Lease lock acquisition minimum refresh delay must be equal or smaller than the maximum refresh delay ('{LeaseLockAcquisitionMaximumRefreshDelay}').");
            }

            LeaseLockAcquisitionMinimumRefreshDelay = value;
        }

        internal TimeSpan LeaseLockAcquisitionTimeout { get; private set; } = TimeSpan.FromSeconds(60);
        internal TimeSpan LeaseLockTime { get; private set; } = TimeSpan.FromSeconds(60);
        internal TimeSpan LeaseLockAcquisitionMaximumRefreshDelay { get; private set; } = TimeSpan.FromMilliseconds(1000);
        internal TimeSpan LeaseLockAcquisitionMinimumRefreshDelay { get; private set; } = TimeSpan.FromMilliseconds(500);
    }
}