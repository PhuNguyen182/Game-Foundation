using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// The spatialisation an entry wants applied to the <c>AudioSource</c> that plays it.
    /// </summary>
    /// <remarks>
    /// Held as authored data rather than configured on a prefab, because voices are pooled: a voice
    /// plays a UI blip one moment and a positioned explosion the next, so every one of these has to
    /// be re-applied on acquire rather than assumed.
    /// </remarks>
    [Serializable]
    public class Audio3DSettings
    {
        [Tooltip("0 is fully 2D, 1 is fully 3D. Everything below is ignored at 0.")] [Range(0f, 1f)] [SerializeField]
        private float spatialBlend;

        [ShowIf("@this.spatialBlend > 0f")]
        [Tooltip("Inside this radius the sound plays at full volume.")]
        [MinValue(0f)]
        [SerializeField]
        private float minDistance = 1f;

        [ShowIf("@this.spatialBlend > 0f")]
        [Tooltip("Beyond this radius the sound stops attenuating further.")]
        [MinValue(0f)]
        [SerializeField]
        private float maxDistance = 500f;

        [ShowIf("@this.spatialBlend > 0f")] [SerializeField]
        private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [ShowIf("@this.spatialBlend > 0f")]
        [Tooltip("How strongly listener and source movement shift the pitch.")]
        [Range(0f, 5f)]
        [SerializeField]
        private float dopplerLevel = 1f;

        [ShowIf("@this.spatialBlend > 0f")]
        [Tooltip("Speaker spread in degrees. 0 keeps the sound a point source.")]
        [Range(0f, 360f)]
        [SerializeField]
        private float spread;

        public float SpatialBlend => this.spatialBlend;
        public float MinDistance => this.minDistance;
        public float MaxDistance => this.maxDistance;
        public AudioRolloffMode RolloffMode => this.rolloffMode;
        public float DopplerLevel => this.dopplerLevel;
        public float Spread => this.spread;

        /// <summary>Whether this entry is positioned in the world at all.</summary>
        public bool Is3D => this.spatialBlend > 0f;

        /// <summary>Writes every value onto <paramref name="source"/>.</summary>
        /// <remarks>
        /// Applied unconditionally, including when <see cref="Is3D"/> is false, so a recycled voice
        /// never inherits the previous sound's falloff.
        /// </remarks>
        public void ApplyTo(AudioSource source)
        {
            source.spatialBlend = this.spatialBlend;
            source.minDistance = this.minDistance;
            source.maxDistance = Mathf.Max(this.minDistance, this.maxDistance);
            source.rolloffMode = this.rolloffMode;
            source.dopplerLevel = this.dopplerLevel;
            source.spread = this.spread;
        }
    }
}