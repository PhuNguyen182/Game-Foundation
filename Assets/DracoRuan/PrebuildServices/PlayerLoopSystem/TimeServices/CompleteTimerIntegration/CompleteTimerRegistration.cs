using System;
using DracoRuan.Foundation.DataFlow.Runtime;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>
    /// Container registration for the Complete Timer feature.
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     DataFlowScope dataFlow = builder.AddDataFlow();
    ///     builder.AddCompleteTimer(dataFlow);
    /// }
    /// </code>
    /// </summary>
    public static class CompleteTimerRegistration
    {
        /// <summary>
        /// Registers the clock, scheduler, production and regeneration registries, the save
        /// repository and the runtime entry point that drives them all.
        /// </summary>
        /// <param name="dataFlow">Scope returned by <c>AddDataFlow</c>; must be registered first.</param>
        public static IContainerBuilder AddCompleteTimer(this IContainerBuilder builder, DataFlowScope dataFlow)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (dataFlow == null) throw new ArgumentNullException(nameof(dataFlow));

            builder.Register<TimerClock>(Lifetime.Singleton)
                .As<ITimeProvider>()
                .AsSelf();

            builder.Register<TimerScheduler>(Lifetime.Singleton);
            builder.Register<ProductionQueueRegistry>(Lifetime.Singleton);
            builder.Register<RegenerationCounterRegistry>(Lifetime.Singleton);

            builder.RegisterDataController<CompleteTimerDataController, TimerSaveDataV1>(
                dataFlow, domainId: CompleteTimerDataController.Id, targetSchemaVersion: 1);

            builder.RegisterEntryPoint<CompleteTimerRuntime>(Lifetime.Singleton);

            return builder;
        }
    }
}
