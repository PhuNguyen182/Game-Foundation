using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tutorial
{
    /// <summary>
    /// Base tutorial controller (REWRITE_PLAN.md mục 5 bước 10 + user instruction: "1 base
    /// tutorial, có sử dụng các UI motion, có 2 chế độ: dựng sẵn kịch bản và truyền data vào").
    /// Belongs on a Tutorial-layer view's root (see REWRITE_PLAN.md 2.2's preset table:
    /// "Tutorial: chặn input ngoài vùng highlight") alongside a UITutorialMaskController.
    ///
    /// Two ways to use it:
    /// - **Scripted scenario**: override RunScenarioAsync() with hand-written steps (arbitrary
    ///   C#, any control flow) - full control, for a tutorial too bespoke for the generic step
    ///   runner below.
    /// - **Data-driven**: leave RunScenarioAsync() at its default, which plays `steps` in order
    ///   through RunStepAsync() - author tutorials as data (UITutorialStep list) without writing
    ///   a line of code per tutorial.
    ///
    /// A subclass picks exactly one mode; RunScenarioAsync's default IS the data-driven path, so
    /// a scripted subclass overrides it and a data-driven one simply assigns `steps` and never
    /// overrides anything.
    /// </summary>
    [RequireComponent(typeof(UITutorialMaskController))]
    public class UITutorialBase : MonoBehaviour
    {
        [SerializeField] private List<UITutorialStep> steps = new List<UITutorialStep>();
        [SerializeField] private Transform resolveRoot;

        private UITutorialMaskController _mask;
        private UniTaskCompletionSource _externalAdvanceSignal;

        protected UITutorialMaskController Mask => this._mask != null ? this._mask : this._mask = this.GetComponent<UITutorialMaskController>();

        /// <summary>Root that UITutorialStep.targetPath resolves against - defaults to this
        /// component's own Transform, same convention as UIMotionTrack.targetPath resolving
        /// against the applying UIMotion's Transform.</summary>
        protected Transform ResolveRoot => this.resolveRoot != null ? this.resolveRoot : this.transform;

        /// <summary>Set before RunAsync() when a tutorial needs to highlight elements on
        /// whatever screen/view happens to be open (the common case - a tutorial almost never
        /// targets its own children) rather than this component's own hierarchy. The tutorial
        /// prefab can't hold a design-time reference to another view's runtime instance, so the
        /// caller (typically the tutorial's own View, right after the target view opens) sets
        /// this once it has that instance in hand.</summary>
        public void SetResolveRoot(Transform root) => this.resolveRoot = root;

        public List<UITutorialStep> Steps
        {
            get => this.steps;
            set => this.steps = value;
        }

        public async UniTask RunAsync() => await this.RunScenarioAsync();

        /// <summary>Override for a scripted scenario. Default plays `Steps` in order via
        /// RunStepAsync - this IS the data-driven mode, not a placeholder for it.</summary>
        protected virtual async UniTask RunScenarioAsync()
        {
            foreach (UITutorialStep step in this.steps)
                await this.RunStepAsync(step);
        }

        /// <summary>
        /// Plays one step: resolves its highlight target, snaps or animates the mask onto it
        /// (via `step.enterMotion` if set - a UIMotion timeline, so a step's entrance can use
        /// any authored track, including a Custom track targeting UITutorialMaskController
        /// itself to move the spotlight - see UITutorialMaskController.PrepareMoveTo), shows
        /// `step.message`, then waits for `step.advanceMode` to say "go".
        /// </summary>
        protected async UniTask RunStepAsync(UITutorialStep step)
        {
            RectTransform target = string.IsNullOrEmpty(step.targetPath)
                ? null
                : this.ResolveRoot.Find(step.targetPath) as RectTransform;

            if (target != null)
            {
                if (step.enterMotion != null)
                {
                    this.Mask.PrepareMoveTo(target, step.maskSprite);
                    await step.enterMotion.PlayShowAsync();
                }
                else
                {
                    this.Mask.SnapTo(target, step.maskSprite);
                }
            }

            this.OnStepMessage(step.message);

            await this.WaitForAdvanceAsync(step);
        }

        /// <summary>Hook for a view to display `message` (e.g. set a dialogue box's text) -
        /// left to the concrete view/subclass since this base has no opinion on dialogue UI
        /// layout.</summary>
        protected virtual void OnStepMessage(string message)
        {
        }

        private async UniTask WaitForAdvanceAsync(UITutorialStep step)
        {
            switch (step.advanceMode)
            {
                case UITutorialAdvanceMode.Timer:
                    await UniTask.Delay(System.TimeSpan.FromSeconds(step.autoAdvanceSeconds));
                    break;

                case UITutorialAdvanceMode.External:
                    this._externalAdvanceSignal = new UniTaskCompletionSource();
                    await this._externalAdvanceSignal.Task;
                    this._externalAdvanceSignal = null;
                    break;

                case UITutorialAdvanceMode.ClickHighlight:
                case UITutorialAdvanceMode.ClickAnywhere:
                default:
                    // Both click modes are driven by a Button/UIButton the concrete view wires
                    // up (on the highlight itself for ClickHighlight - it's already raycastable
                    // since it's inside the mask's hole - or on a "tap to continue" prompt for
                    // ClickAnywhere) calling AdvanceCurrentStep(); this base doesn't assume a
                    // specific input component.
                    this._externalAdvanceSignal = new UniTaskCompletionSource();
                    await this._externalAdvanceSignal.Task;
                    this._externalAdvanceSignal = null;
                    break;
            }
        }

        /// <summary>Call from a click handler (or any external gameplay signal) to unblock the
        /// step currently waiting on it. A no-op if no step is currently waiting.</summary>
        public void AdvanceCurrentStep() => this._externalAdvanceSignal?.TrySetResult();
    }
}
