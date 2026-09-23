using System;
using DracoRuan.PrebuildServices.MobileNotification.Core;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileNotification.Installer
{
    /// <summary>
    /// Handle returned by <see cref="MobileNotificationServiceRegistration.AddMobileNotificationService"/>.
    /// </summary>
    public sealed class MobileNotificationServiceScope
    {
        public MobileNotificationServiceScope(MobileNotificationConfig config) => this.Config = config;

        public MobileNotificationConfig Config { get; }
    }

    /// <summary>
    /// Container registration for the notification service, shaped like
    /// <c>VibrationServiceRegistration</c> so both are declared the same way:
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     builder.AddMobileNotificationService(this.notificationConfig);
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <b>Registered through <c>RegisterEntryPoint</c>.</b> <see cref="MobileNotificationService"/>
    /// implements VContainer's <c>IStartable</c> and <c>ITickable</c>, which are only called on types
    /// registered as entry points. The config is passed as a constructor parameter rather than as a
    /// container instance, so a missing config reaches the service, which logs it and falls back to
    /// defaults, instead of failing the whole container build.
    /// </remarks>
    public static class MobileNotificationServiceRegistration
    {
        /// <summary>Registers the notification service and returns a handle describing what it was given.</summary>
        public static MobileNotificationServiceScope AddMobileNotificationService(
            this IContainerBuilder builder, MobileNotificationConfig config)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // RegisterEntryPoint<T>(...) is builder.Register<T>(lifetime).AsImplementedInterfaces()
            // plus the EntryPointDispatcher, so IMobileNotificationService, IAsyncInitializable,
            // IStartable, ITickable and IDisposable all resolve from this one call.
            builder.RegisterEntryPoint<MobileNotificationService>(Lifetime.Singleton)
                .WithParameter(typeof(MobileNotificationConfig), config)
                .AsSelf();

            return new MobileNotificationServiceScope(config);
        }
    }
}
