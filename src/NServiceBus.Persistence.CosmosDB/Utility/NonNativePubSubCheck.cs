namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using NServiceBus.Features;

    class NonNativePubSubCheck
    {
        public static void ThrowIfMessageDrivenPubSubInUse(FeatureConfigurationContext context)
        {
#pragma warning disable CS0618
            if (context.Settings.IsFeatureEnabled(typeof(MessageDrivenSubscriptions)) || context.Settings.IsFeatureActive(typeof(MessageDrivenSubscriptions)))
#pragma warning restore CS0618
            {
                throw new Exception("NServiceBus.Persistence.CosmosDB must be used with a transport that provides native publish/subscribe capabilities, and cannot be used with message-driven publish/subscribe.");
            }
        }
    }
}
