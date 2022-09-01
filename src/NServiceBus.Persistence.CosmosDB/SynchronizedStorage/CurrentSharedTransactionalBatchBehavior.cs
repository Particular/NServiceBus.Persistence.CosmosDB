namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class CurrentSharedTransactionalBatchBehavior : IBehavior<IIncomingLogicalMessageContext,
        IIncomingLogicalMessageContext>
    {
        public CurrentSharedTransactionalBatchBehavior(
            CurrentSharedTransactionalBatchHolder currentTransactionalBatchHolder)
        {
            this.currentTransactionalBatchHolder = currentTransactionalBatchHolder;
        }

        public async Task Invoke(IIncomingLogicalMessageContext context,
            Func<IIncomingLogicalMessageContext, Task> next)
        {
            using (currentTransactionalBatchHolder.CreateScope())
            {
                await next(context).ConfigureAwait(false);
            }
        }

        readonly CurrentSharedTransactionalBatchHolder currentTransactionalBatchHolder;

        public class Registration : RegisterStep
        {
            public Registration() : base("CurrentSharedTransactionalBatchBehavior",
                typeof(CurrentSharedTransactionalBatchBehavior),
                "Manages the lifecycle of the current storage session.",
                b => new CurrentSharedTransactionalBatchBehavior(b.Build<CurrentSharedTransactionalBatchHolder>()))
            {
            }
        }
    }
}