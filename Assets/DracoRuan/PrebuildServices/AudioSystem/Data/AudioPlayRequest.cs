using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// Per-call overrides for one play request. Everything is optional.
    /// </summary>
    /// <remarks>
    /// <para>A <c>default</c> request means "play this entry exactly as authored", so the common
    /// call site stays <c>audio.Play(AudioId.Click)</c> and nothing here has to be thought about
    /// until it matters. Set only what you want to change:
    /// <code>
    /// audio.Play(AudioId.Explosion, new AudioPlayRequest { Position = hit.point });
    /// </code></para>
    ///
    /// <para>Plain fields rather than init-only properties: Unity's reference assemblies have no
    /// <c>IsExternalInit</c>, so <c>init</c> does not compile here. Fields also mean that reading
    /// through an <c>in</c> parameter does not make a defensive copy.</para>
    ///
    /// <para>The values where zero is meaningful are nullable rather than sentinel-zero. Starting a
    /// voice at volume 0 to fade it up is a real request, and a struct that could not express it
    /// would push callers into the fade API for something simple.</para>
    /// </remarks>
    public struct AudioPlayRequest
    {
        /// <summary>Multiplies the entry's authored volume. Null keeps it.</summary>
        public float? VolumeScale;

        /// <summary>Multiplies the entry's authored pitch. Null keeps it.</summary>
        public float? PitchScale;

        /// <summary>Where in the world to place the voice. Null leaves it unpositioned.</summary>
        public Vector3? Position;

        /// <summary>A transform the voice follows for as long as it plays.</summary>
        public Transform FollowTarget;

        /// <summary>Fade the voice up from silence over this many seconds.</summary>
        public float FadeInSeconds;

        /// <summary>Shape of that fade-in.</summary>
        public AudioFadeCurveType FadeCurve;

        /// <summary>Wait this long before the first sample.</summary>
        public float DelaySeconds;

        /// <summary>Start this far into the clip.</summary>
        public float StartTimeSeconds;

        /// <summary>Overrides the entry's loop flag. Null keeps it.</summary>
        public bool? LoopOverride;

        /// <summary>Routes this one instance to a different channel. Null keeps the entry's.</summary>
        public string ChannelOverride;

        /// <summary>Overrides the entry's priority. Null keeps it.</summary>
        public int? PriorityOverride;

        /// <summary>
        /// Skips the fire-rate and concurrency gate. For UI and scripted one-offs that must be
        /// heard, where the throttle a designer set for the same sound in gameplay would be wrong.
        /// </summary>
        public bool IgnoreFireRate;
    }
}
