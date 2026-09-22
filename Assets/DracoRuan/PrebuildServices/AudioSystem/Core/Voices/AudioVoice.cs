using System.Threading;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Voices
{
    /// <summary>
    /// One pooled <c>AudioSource</c> and everything the service needs to know about what it is
    /// currently playing.
    /// </summary>
    /// <remarks>
    /// <para>A plain class holding a GameObject, not a MonoBehaviour. Voices are acquired and
    /// released constantly, and the project's update system keeps a static list with index
    /// bookkeeping that is not safe to churn: registering and deregistering dozens of handlers a
    /// second there causes <i>unrelated</i> handlers to be skipped. The whole audio system is one
    /// tick handler, and voices are data it walks.</para>
    ///
    /// <para>Channel and master volume are not multiplied in here. They live in the mixer, which is
    /// what makes "set music to 50%" one parameter write instead of a walk over every live voice.
    /// </para>
    /// </remarks>
    public sealed class AudioVoice
    {
        public AudioVoice(int slotIndex, Transform root)
        {
            this.SlotIndex = slotIndex;

            this.GameObject = new GameObject($"AudioVoice_{slotIndex:00}");
            this.GameObject.transform.SetParent(root, worldPositionStays: false);
            this.GameObject.SetActive(false);

            this.Transform = this.GameObject.transform;
            this.Source = this.GameObject.AddComponent<AudioSource>();
            this.Source.playOnAwake = false;

            this.State = AudioPlaybackState.Free;
        }

        public int SlotIndex { get; }
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public AudioSource Source { get; }

        public AudioPlaybackState State { get; set; }

        /// <summary>Which entry is playing, as the database's index.</summary>
        public int EntryIndex { get; set; }

        /// <summary>Which channel this voice is routed to, resolved once at acquire.</summary>
        public int ChannelIndex { get; set; }

        public AudioEntry Entry { get; set; }

        /// <summary>The entry's volume for this play, after randomisation and request scaling.</summary>
        public float BaseVolume { get; set; }

        /// <summary>0..1, owned by the fade engine.</summary>
        public float FadeVolume { get; set; }

        /// <summary>The entry's pitch for this play, before channel and master pitch.</summary>
        public float BasePitch { get; set; }

        public int Priority { get; set; }

        public float StartTime { get; set; }

        /// <summary>When this voice will have finished, on the service clock.</summary>
        public float ExpectedEndTime { get; set; }

        /// <summary>When it was paused, so resuming can push <see cref="ExpectedEndTime"/> back.</summary>
        public float PausedAtTime { get; set; }

        public bool IsOneShot { get; set; }

        public bool IsLoop { get; set; }

        /// <summary>The incoming side of a cross-fade. Never stolen or replaced while true.</summary>
        public bool IsProtected { get; set; }

        /// <summary>
        /// Pause was asked for while the clip was still loading, so the voice must arrive paused
        /// rather than audible.
        /// </summary>
        public bool PauseOnStart { get; set; }

        public Transform FollowTarget { get; set; }

        /// <summary>Cancels this voice's in-flight clip load when it is released or stolen.</summary>
        public CancellationTokenSource LoadCts { get; set; }

        /// <summary>Pushes the current volume to the source.</summary>
        public void ApplyVolume() => this.Source.volume = this.BaseVolume * this.FadeVolume;

        /// <summary>Pushes the current pitch to the source, including channel and master.</summary>
        public void ApplyPitch(float channelPitch, float masterPitch) =>
            this.Source.pitch = this.BasePitch * channelPitch * masterPitch;

        /// <summary>Returns the voice to a known-clean state, ready to be handed out again.</summary>
        public void ResetForReuse()
        {
            this.Source.Stop();
            this.Source.clip = null;
            this.Source.loop = false;
            this.Source.outputAudioMixerGroup = null;
            this.Source.panStereo = 0f;
            this.Source.time = 0f;
            this.GameObject.SetActive(false);

            this.State = AudioPlaybackState.Free;
            this.Entry = null;
            this.EntryIndex = -1;
            this.ChannelIndex = -1;
            this.BaseVolume = 1f;
            this.FadeVolume = 1f;
            this.BasePitch = 1f;
            this.Priority = 128;
            this.StartTime = 0f;
            this.ExpectedEndTime = 0f;
            this.PausedAtTime = 0f;
            this.IsOneShot = false;
            this.IsLoop = false;
            this.IsProtected = false;
            this.PauseOnStart = false;
            this.FollowTarget = null;
            this.LoadCts = null;
        }
    }
}
