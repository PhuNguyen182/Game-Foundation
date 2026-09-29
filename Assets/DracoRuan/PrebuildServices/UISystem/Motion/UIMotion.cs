using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Standalone animation component: two timelines (Show/Hide), each a flat list of
    /// UIMotionTrack. Does not reference UISystem runtime - works on any GameObject.
    /// See REWRITE_PLAN.md section 2.7 for the full spec. Covers Show/Hide playback,
    /// rest-pose capture/restart, "new Play stops the old one in place",
    /// OnEnable/OnParentShow/Manual triggers, Mirror Hide, cancellation-token
    /// snap-to-end, HideAndDeactivateAsync/SetActiveAnimated, reduce-motion
    /// (UIMotionRunner.ReduceMotion), stagger (one authored track fans out into one
    /// clone per direct child of `target`, see ExpandStagger). Every UIMotion owns its own
    /// inline tracks - there is no shared preset asset. All 12 UIMotionTrackKind values
    /// have an evaluator (see UIMotionTrackEvaluator/UIMotionRunner). NOT yet implemented:
    /// validators.
    /// </summary>
    public partial class UIMotion : MonoBehaviour
    {
        [SerializeField] private List<UIMotionTrack> showTracks = new List<UIMotionTrack>();
        [SerializeField] private List<UIMotionTrack> hideTracks = new List<UIMotionTrack>();
        [SerializeField] private UIMotionTrigger trigger = UIMotionTrigger.OnEnable;
        [SerializeField] private float speedOverride = 1f;

        /// <summary>When true, Hide plays a timeline generated from Show (reversed
        /// schedule, reversed ease, from/to swapped) instead of `hideTracks`. See
        /// BuildMirrorHideTimeline for the exact per-track rule.</summary>
        [SerializeField] private bool mirrorHide;

        private bool _prepared;

        /// <summary>Authored showTracks/hideTracks with every `stagger` track expanded
        /// into one concrete clone per direct child of its target (ExpandStagger). This,
        /// not the authored lists, is what actually gets captured/played/mirrored -
        /// computed once (cached, per the plan's "danh sách con cho stagger cũng được
        /// cache lúc Awake") and only recomputed by EnsurePrepared/ConfigureForTest.</summary>
        private UIMotionTrack[] _showExpanded;

        private UIMotionTrack[] _hideExpanded;

        private Vector4[] _showRestPoses;
        private Vector4[] _hideRestPoses;
        private UIMotionPlaybackHandle _currentHandle;
        private UIMotionRunner.Playback _lastShowPlayback;
        private IUIMotionTriggerSource _triggerSource;

        private void Awake() => this.EnsurePrepared();

        private void OnEnable()
        {
            switch (this.trigger)
            {
                case UIMotionTrigger.OnEnable:
                    this.Restart();
                    break;
                case UIMotionTrigger.OnParentShow:
                    this._triggerSource = this.GetComponentInParent<IUIMotionTriggerSource>();
                    if (this._triggerSource != null)
                        this._triggerSource.ParentShown += this.OnParentShown;
                    break;
            }
        }

        private void OnDisable()
        {
            if (this._triggerSource != null)
            {
                this._triggerSource.ParentShown -= this.OnParentShown;
                this._triggerSource = null;
            }

            this.StopCurrent();
        }

        private void OnParentShown() => this.Restart();

        /// <summary>
        /// Captures rest poses and registers with the runner if that hasn't happened yet.
        /// Awake does this for the common case, but a UIMotion whose GameObject was never
        /// activated (Awake never ran) can still be driven via script - every public entry
        /// point below calls this first so _showRestPoses/_hideRestPoses is never null.
        /// </summary>
        private void EnsurePrepared()
        {
            if (this._prepared)
                return;

            this._prepared = true;
            UIMotionRunner.EnsureRegistered();
            this.PrepareTimelines();
        }

        /// <summary>Expands stagger tracks and captures rest poses against the result.
        /// Shared by EnsurePrepared and ConfigureForTest so the two never drift.</summary>
        private void PrepareTimelines()
        {
            this._showExpanded = ExpandStagger(this.WithSelfTargets(this.showTracks));
            this._hideExpanded = ExpandStagger(this.WithSelfTargets(this.hideTracks));
            ResolveTargetCaches(this._showExpanded);
            ResolveTargetCaches(this._hideExpanded);
            this._showRestPoses = CaptureRestPoses(this._showExpanded);
            this._hideRestPoses = CaptureRestPoses(this._hideExpanded);
        }

        /// <summary>A track whose `target` was left empty animates this UIMotion's own GameObject,
        /// so the common "animate myself" case needs no drag-and-drop. Returns `tracks` itself when
        /// nothing needs replacing; otherwise a copy where each empty-target track is a Clone
        /// pointing at this GameObject (the authored track is never modified).</summary>
        private List<UIMotionTrack> WithSelfTargets(List<UIMotionTrack> tracks)
        {
            List<UIMotionTrack> resolved = null;
            for (int i = 0; i < tracks.Count; i++)
            {
                // ReferenceEquals: only a truly unset field. A reference to a since-destroyed
                // object must stay inert instead of silently retargeting to this object.
                if (!ReferenceEquals(tracks[i].target, null))
                    continue;

                resolved ??= new List<UIMotionTrack>(tracks);
                UIMotionTrack self = tracks[i].Clone();
                self.target = this.gameObject;
                resolved[i] = self;
            }

            return resolved ?? tracks;
        }

        /// <summary>Resolves every track's UIMotionTrack.ResolvedXxx component cache once,
        /// covering every shape PrepareTimelines can produce - authored tracks (passed
        /// through unchanged by ExpandStagger) and stagger's per-child clones - so
        /// CaptureRestPoses (right below) and every later tick already read from the cache
        /// instead of the first tick re-resolving it.</summary>
        private static void ResolveTargetCaches(UIMotionTrack[] tracks)
        {
            for (int i = 0; i < tracks.Length; i++)
                UIMotionTrackEvaluator.ResolveTargetCache(tracks[i]);
        }

        /// <summary>Snaps every track (both timelines) back to its rest pose, then plays Show.</summary>
        public void Restart()
        {
            this.EnsurePrepared();
            this.StopCurrent();
            SnapToPoses(this._showExpanded, this._showRestPoses);
            SnapToPoses(this._hideExpanded, this._hideRestPoses);
            this.PlayShowAsync().Forget();
        }

        public UniTask<UIMotionPlaybackResult> PlayShowAsync(CancellationToken ct = default)
        {
            this.EnsurePrepared();
            return this.PlayCore(this._showExpanded, this._showRestPoses, null, ct, isShowTimeline: true);
        }

        public UniTask<UIMotionPlaybackResult> PlayHideAsync(CancellationToken ct = default)
        {
            this.EnsurePrepared();

            if (this.mirrorHide)
            {
                (UIMotionTrack[] tracks, bool[] mirrorEase) = this.BuildMirrorHideTimeline();
                return this.PlayCore(tracks, this._showRestPoses, mirrorEase, ct, isShowTimeline: false);
            }

            return this.PlayCore(this._hideExpanded, this._hideRestPoses, null, ct, isShowTimeline: false);
        }

        /// <summary>Plays Hide and, once it completes (or is replaced by a newer play),
        /// deactivates the GameObject. Unity does not let SetActive(false) itself be
        /// delayed, so waiting for Hide to finish before deactivating requires an
        /// explicit async entry point instead.</summary>
        public async UniTask HideAndDeactivateAsync(CancellationToken ct = default)
        {
            await this.PlayHideAsync(ct);
            if (this != null)
                this.gameObject.SetActive(false);
        }

        /// <summary>Fire-and-forget convenience: activates then plays Show, or plays Hide
        /// then deactivates. See HideAndDeactivateAsync for why the deactivate case needs
        /// to await Hide first rather than calling SetActive(false) directly.</summary>
        public void SetActiveAnimated(bool active)
        {
            if (active)
            {
                this.gameObject.SetActive(true);
                this.PlayShowAsync().Forget();
            }
            else
            {
                this.HideAndDeactivateAsync().Forget();
            }
        }

        public void Stop() => this.StopCurrent();

        public void SnapShow()
        {
            this.EnsurePrepared();
            this.StopCurrent();
            SnapToEndValues(this._showExpanded, this._showRestPoses);
        }

        public void SnapHide()
        {
            this.EnsurePrepared();
            this.StopCurrent();

            if (this.mirrorHide)
            {
                (UIMotionTrack[] tracks, _) = this.BuildMirrorHideTimeline();
                SnapToEndValues(tracks, this._showRestPoses);
            }
            else
            {
                SnapToEndValues(this._hideExpanded, this._hideRestPoses);
            }
        }

        /// <summary>Test-only setup hook (see Motion/AssemblyInfo.cs InternalsVisibleTo):
        /// assigns the timelines directly instead of through the Inspector, then captures
        /// rest poses immediately (not lazily on the next EnsurePrepared) so a caller can
        /// change a target's live value right after this call without it leaking into the
        /// captured rest pose.</summary>
        internal void ConfigureForTest(
            IEnumerable<UIMotionTrack> show, IEnumerable<UIMotionTrack> hide,
            bool mirrorHide = false, float speedOverride = 1f, UIMotionTrigger trigger = UIMotionTrigger.Manual)
        {
            this.showTracks = new List<UIMotionTrack>(show);
            this.hideTracks = new List<UIMotionTrack>(hide);
            this.mirrorHide = mirrorHide;
            this.speedOverride = speedOverride;
            this.trigger = trigger;

            this._prepared = true;
            UIMotionRunner.EnsureRegistered();
            this.PrepareTimelines();
        }

        /// <summary>Test-only inspection hook: the actual expanded Show timeline
        /// (post-stagger) that PlayShowAsync would play, for asserting on stagger's
        /// per-child target/offset without needing the PlayerLoop to tick.</summary>
        internal IReadOnlyList<UIMotionTrack> ShowExpandedForTest => this._showExpanded;

        private UniTask<UIMotionPlaybackResult> PlayCore(
            IReadOnlyList<UIMotionTrack> tracks, Vector4[] restPoses, bool[] mirrorEase,
            CancellationToken ct, bool isShowTimeline)
        {
            this.StopCurrent();

            UIMotionPlaybackHandle handle = UIMotionRunner.Play(tracks, restPoses, this.speedOverride, mirrorEase);
            this._currentHandle = handle;
            if (isShowTimeline)
                this._lastShowPlayback = handle.Playback;

            CancellationTokenRegistration ctRegistration = ct.CanBeCanceled
                ? ct.Register(() => handle.SnapToEnd())
                : default;

            return AwaitAndClear(this, handle, ctRegistration);

            static async UniTask<UIMotionPlaybackResult> AwaitAndClear(
                UIMotion self, UIMotionPlaybackHandle handle, CancellationTokenRegistration ctRegistration)
            {
                UIMotionPlaybackResult result = await handle.Task;
                ctRegistration.Dispose();
                if (self._currentHandle == handle)
                    self._currentHandle = null;
                return result;
            }
        }

        /// <summary>
        /// Stops the current playback, tolerant of a Stop() continuation re-entrantly
        /// starting a new one (its completion source resumes awaiters inline). Detaches
        /// _currentHandle before calling Stop() so that reentrant continuation sees no
        /// "current" handle to clobber; if it starts a new playback anyway, that one is
        /// stopped too, since whoever called this method is about to become current.
        /// </summary>
        private void StopCurrent()
        {
            UIMotionPlaybackHandle handle = this._currentHandle;
            this._currentHandle = null;
            handle?.Stop();

            if (this._currentHandle != null)
            {
                UIMotionPlaybackHandle reentrant = this._currentHandle;
                this._currentHandle = null;
                reentrant.Stop();
            }
        }

        /// <summary>
        /// Builds a synthetic Hide timeline from the expanded Show timeline (post-stagger,
        /// see ExpandStagger), per the plan's per-track
        /// Mirror rule (REWRITE_PLAN.md section 2.7, "Mirror Show xử lý theo từng track"):
        /// - useStartValue track: Hide runs Target -> Start (both already fully resolved,
        ///   independent of whether Show ever actually ran).
        /// - non-useStartValue track: Hide runs from its own live current value (same as
        ///   any other non-useStartValue track) to a snapshot of the value Show's track
        ///   captured at the moment IT started - or Rest if Show never got that far (or
        ///   never played at all).
        /// The whole timeline's schedule is time-reversed (a track that started last in
        /// Show starts first in Hide) and every track's ease is played backward
        /// (1 - ease(1 - t)) - a ruling beyond the two bulleted cases above, which only
        /// call out ease reversal for the useStartValue branch; applying it uniformly is
        /// what makes the Hide timeline read as "Show in reverse" rather than a Show
        /// reversal for some tracks and a fresh tween for others.
        /// </summary>
        private (UIMotionTrack[] tracks, bool[] mirrorEase) BuildMirrorHideTimeline()
        {
            UIMotionTrack[] show = this._showExpanded;
            int n = show.Length;
            var tracks = new UIMotionTrack[n];
            var mirrorEase = new bool[n];

            var scheduleInputs = new UIMotionScheduleInput[n];
            for (int i = 0; i < n; i++)
            {
                UIMotionTrack source = show[i];
                scheduleInputs[i] = new UIMotionScheduleInput(source.startMode, source.offset, source.duration);
            }

            float[] showStartTimes = UIMotionScheduler.ComputeStartTimes(scheduleInputs);
            float showTotalDuration = UIMotionScheduler.ComputeTotalDuration(scheduleInputs, showStartTimes);

            UIMotionRunner.Playback lastShow = this._lastShowPlayback;

            for (int i = 0; i < n; i++)
            {
                UIMotionTrack source = show[i];
                Vector4 rest = this._showRestPoses[i];

                UIMotionTrack mirrored = source.Clone();
                mirrored.startMode = UIMotionStartMode.AtTime;
                mirrored.offset = showTotalDuration - (showStartTimes[i] + source.duration);

                // Clone() deliberately does not carry the resolved-target cache over (see
                // its own remarks) since a clone can point `target` somewhere new - but
                // this one doesn't; `target` is unchanged from `source`, only the
                // schedule/from/to move below. Still needs its own resolve rather than
                // reading source's cache directly: TickTracks/Write index into `mirrored`
                // (the array returned here), never back into `source`.
                UIMotionTrackEvaluator.ResolveTargetCache(mirrored);

                if (source.kind == UIMotionTrackKind.Punch || source.kind == UIMotionTrackKind.Shake
                                                           || source.kind == UIMotionTrackKind.Custom ||
                                                           source.kind == UIMotionTrackKind.AnimatorState)
                {
                    // Self-mirroring: Punch/Shake oscillate around "wherever it happens to
                    // be" with no direction to reverse, Custom owns its own from/to through
                    // IUIMotionCustomTrack, and AnimatorState's pose comes entirely from the
                    // clip (plan: "Mirror Show không áp dụng được cho Animator track" - an
                    // Editor validator, not built yet, is meant to reject this combination
                    // at author time; this is just the runtime not crashing on it). Clone()
                    // already carried target/to/animator*/etc. through unchanged; only the
                    // schedule (offset above) moves.
                    mirrored.useStartValue = false;
                }
                else if (source.useStartValue)
                {
                    (Vector4 from, Vector4 to) = ResolveAuthoredRange(source, rest);
                    mirrored.useStartValue = true;
                    mirrored.fromValueMode = UIMotionValueMode.Absolute;
                    mirrored.from = to;
                    mirrored.toValueMode = UIMotionValueMode.Absolute;
                    mirrored.to = from;
                }
                else
                {
                    bool started = lastShow != null
                                   && i < lastShow.trackStates.Length
                                   && lastShow.trackStates[i].started;
                    Vector4 snapshot = started ? lastShow.trackStates[i].resolvedFrom : rest;

                    mirrored.useStartValue = false;
                    mirrored.toValueMode = UIMotionValueMode.Absolute;
                    mirrored.to = snapshot;
                }

                tracks[i] = mirrored;
                mirrorEase[i] = true;
            }

            return (tracks, mirrorEase);
        }

        /// <summary>Resolves a track's authored Start/Target purely from its own data and
        /// the rest pose - the same result Show would produce for this track if it had
        /// never actually run (no dependency on runtime capture).</summary>
        private static (Vector4 from, Vector4 to) ResolveAuthoredRange(UIMotionTrack track, Vector4 rest)
        {
            Float4 restF = rest.ToFloat4();
            Float4 parentSize = UIMotionTrackEvaluator.GetParentSize(track).ToFloat4();
            bool multiplicative = track.kind == UIMotionTrackKind.Scale;

            Float4 from = track.useStartValue
                ? UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), restF, restF, parentSize,
                    multiplicative)
                : restF;
            Float4 to = UIMotionValueResolver.Resolve(track.toValueMode, track.to.ToFloat4(), restF, from, parentSize,
                multiplicative);

            return (from.ToVector4(), to.ToVector4());
        }

        /// <summary>
        /// Expands every `stagger` track in `authored` into one clone per direct child of
        /// its target (REWRITE_PLAN.md 2.7: "Stagger: mỗi object con tự chụp giá trị hiện
        /// tại của riêng nó") - each clone keeps the source's kind/ease/duration/value
        /// config but points `target` at one child and starts at the source's own
        /// (pre-stagger) schedule position plus `childIndex * staggerDelay`. A non-stagger
        /// track passes through unchanged. Ruling: a track's own position in the timeline
        /// (via WithPrevious/AfterPrevious/AtTime) is computed against the AUTHORED list
        /// exactly as if stagger didn't exist - stagger only fans out that one track's own
        /// playback, it does not change how later tracks schedule relative to it. A target
        /// with no children expands to zero tracks (inert, not an error).
        /// </summary>
        private static UIMotionTrack[] ExpandStagger(List<UIMotionTrack> authored)
        {
            var scheduleInputs = new UIMotionScheduleInput[authored.Count];
            for (int i = 0; i < authored.Count; i++)
                scheduleInputs[i] =
                    new UIMotionScheduleInput(authored[i].startMode, authored[i].offset, authored[i].duration);
            float[] baseStartTimes = UIMotionScheduler.ComputeStartTimes(scheduleInputs);

            var result = new List<UIMotionTrack>(authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                UIMotionTrack source = authored[i];
                if (!source.stagger)
                {
                    result.Add(source);
                    continue;
                }

                int childCount = UIMotionTrackEvaluator.GetChildCount(source);
                for (int c = 0; c < childCount; c++)
                {
                    UIMotionTrack clone = source.Clone();
                    clone.target = UIMotionTrackEvaluator.GetChild(source, c);
                    clone.startMode = UIMotionStartMode.AtTime;
                    clone.offset = baseStartTimes[i] + c * source.staggerDelay;
                    clone.stagger = false; // this is now one concrete per-child track, not itself staggered
                    result.Add(clone);
                }
            }

            return result.ToArray();
        }

        private static Vector4[] CaptureRestPoses(IReadOnlyList<UIMotionTrack> tracks)
        {
            var result = new Vector4[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
                result[i] = UIMotionTrackEvaluator.Capture(tracks[i]);
            return result;
        }

        private static void SnapToPoses(IReadOnlyList<UIMotionTrack> tracks, Vector4[] poses)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                UIMotionTrack track = tracks[i];
                if (track.kind == UIMotionTrackKind.Custom)
                {
                    if (UIMotionTrackEvaluator.TryGetCustomTrack(track, out IUIMotionCustomTrack custom))
                        custom.Snap(toEnd: false);
                    continue;
                }

                if (track.kind == UIMotionTrackKind.AnimatorState)
                {
                    SnapAnimatorTrack(track, toEnd: false);
                    continue;
                }

                UIMotionTrackEvaluator.Write(track, poses[i]);
            }
        }

        /// <summary>REWRITE_PLAN.md 2.7: "Snap về cuối: Play(stateHash, layer, 1f) +
        /// Update(0f). Reset về đầu: Play(stateHash, layer, 0f) + Update(0f). Cả hai làm
        /// trong lúc Animator đang bật, sau đó mới tắt."</summary>
        private static void SnapAnimatorTrack(UIMotionTrack track, bool toEnd)
        {
            if (!UIMotionTrackEvaluator.TryGetAnimator(track, out Animator animator))
                return;

            animator.enabled = true;
            animator.Play(track.animatorStateHash, track.animatorLayer, toEnd ? 1f : 0f);
            animator.Update(0f);
            animator.enabled = false;
        }

        private static void SnapToEndValues(IReadOnlyList<UIMotionTrack> tracks, Vector4[] restPoses)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                UIMotionTrack track = tracks[i];

                // Punch/Shake always decay back to exactly the value they started from -
                // there is no authored Start/Target to resolve, so "snap to end" is just
                // "leave the rest pose alone".
                if (track.kind == UIMotionTrackKind.Punch || track.kind == UIMotionTrackKind.Shake)
                {
                    UIMotionTrackEvaluator.Write(track, restPoses[i]);
                    continue;
                }

                if (track.kind == UIMotionTrackKind.Custom)
                {
                    if (UIMotionTrackEvaluator.TryGetCustomTrack(track, out IUIMotionCustomTrack custom))
                        custom.Snap(toEnd: true);
                    continue;
                }

                if (track.kind == UIMotionTrackKind.AnimatorState)
                {
                    SnapAnimatorTrack(track, toEnd: true);
                    continue;
                }

                Float4 rest = restPoses[i].ToFloat4();
                Float4 parentSize = UIMotionTrackEvaluator.GetParentSize(track).ToFloat4();
                bool multiplicative = track.kind == UIMotionTrackKind.Scale;

                Float4 start = track.useStartValue
                    ? UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize,
                        multiplicative)
                    : rest;
                Float4 end = UIMotionValueResolver.Resolve(track.toValueMode, track.to.ToFloat4(), rest, start,
                    parentSize, multiplicative);

                Vector4 endVector = end.ToVector4();
                if (track.kind == UIMotionTrackKind.Rotate)
                    endVector = UIMotionTrackEvaluator.UnwrapRotation(start.ToVector4(), endVector);

                UIMotionTrackEvaluator.Write(track, endVector);
            }
        }
    }
}