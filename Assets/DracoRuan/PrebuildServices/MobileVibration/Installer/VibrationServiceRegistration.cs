using System;
using DracoRuan.PrebuildServices.MobileVibration.Core;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileVibration.Installer
{
    /// <summary>
    /// Handle returned by <see cref="VibrationServiceRegistration.AddVibrationService"/>.
    /// </summary>
    public sealed class VibrationServiceScope
    {
        public VibrationServiceScope(VibrationCollection collection) => this.Collection = collection;

        public VibrationCollection Collection { get; }
    }

    /// <summary>
    /// Container registration for the vibration service, shaped like <c>AudioServiceRegistration</c>
    /// so both are declared the same way:
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     builder.AddVibrationService(this.vibrationCollection);
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <b>Registered through <c>RegisterEntryPoint</c>, not a plain <c>Register</c>.</b>
    /// <c>AudioService</c> gets away with a plain
    /// <c>builder.Register&lt;AudioService&gt;(...).As&lt;IAsyncInitializable&gt;()</c> because it
    /// drives its own per-frame updates through <c>UpdateServiceManager</c>, a custom tick system
    /// that has nothing to do with VContainer. <see cref="VibrationService"/> instead implements
    /// VContainer's own <c>ITickable</c>, and VContainer only calls <c>Tick()</c> on types registered
    /// through <c>RegisterEntryPoint</c> — that is what wires a type into its
    /// <c>EntryPointDispatcher</c>. A plain <c>Register(...).As&lt;ITickable&gt;()</c> is never
    /// ticked. <c>MobileNotificationService</c>, the project's other <c>ITickable</c> service, is
    /// registered the same way in <c>MobileNotificationServiceRegistration</c>.
    /// </remarks>
    public static class VibrationServiceRegistration
    {
        /// <summary>Registers the vibration service and returns a handle describing what it was given.</summary>
        public static VibrationServiceScope AddVibrationService(
            this IContainerBuilder builder, VibrationCollection collection)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (collection)
                builder.RegisterInstance(collection);

            // RegisterEntryPoint<T>(...) is builder.Register<T>(lifetime).AsImplementedInterfaces()
            // plus ensuring the EntryPointDispatcher exists, so IVibrationService, ITickable and
            // IDisposable are all resolvable from this one call. AsSelf() on top also lets the
            // concrete VibrationService be resolved directly, matching AudioServiceRegistration.
            builder.RegisterEntryPoint<VibrationService>(Lifetime.Singleton)
                .AsSelf();

            return new VibrationServiceScope(collection);
        }
    }
}
