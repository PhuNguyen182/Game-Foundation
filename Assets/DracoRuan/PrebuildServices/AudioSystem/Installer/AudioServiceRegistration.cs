using System;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.AudioSystem.Core;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Interfaces;
using VContainer;

namespace DracoRuan.PrebuildServices.AudioSystem.Installer
{
    /// <summary>
    /// Handle returned by <see cref="AudioServiceRegistration.AddAudioService"/>.
    /// </summary>
    public sealed class AudioServiceScope
    {
        public AudioServiceScope(AudioConfig config, AudioCollection collection)
        {
            this.Config = config;
            this.Collection = collection;
        }

        public AudioConfig Config { get; }
        public AudioCollection Collection { get; }
    }

    /// <summary>
    /// Container registration for the audio service, shaped like <c>StaticDataRegistration</c> so
    /// both are declared the same way.
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     builder.AddAudioService(this.audioConfig, this.audioCollection);
    /// }
    /// </code>
    /// </summary>
    public static class AudioServiceRegistration
    {
        /// <summary>
        /// Registers the audio service and returns a handle describing what it was given.
        /// </summary>
        /// <remarks>
        /// <b>The <c>IAsyncInitializable</c> registration is load-bearing.</b> Without it the boot
        /// pipeline does not wait for the database index or for preloads, and the first scene can
        /// ask for a sound before the service knows any.
        /// </remarks>
        public static AudioServiceScope AddAudioService(
            this IContainerBuilder builder, AudioConfig config, AudioCollection collection)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.RegisterInstance(config);

            if (collection != null)
                builder.RegisterInstance(collection);

            builder.Register<AudioService>(Lifetime.Singleton)
                .As<IAudioService>()
                .As<IAsyncInitializable>()
                .AsSelf();

            return new AudioServiceScope(config, collection);
        }
    }
}
