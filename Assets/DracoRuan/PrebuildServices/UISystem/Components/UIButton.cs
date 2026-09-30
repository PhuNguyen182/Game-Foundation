using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Motion;
using R3;
using UnityEngine;
using UnityEngine.UI;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Concrete click component (REWRITE_PLAN.md 2.5): does not inherit UIViewBase, wraps a
    /// Button on the same GameObject. Cooldown is an optional secondary guard (default 0,
    /// unscaled time) - double-tap protection belongs to AsyncUICommand.IsExecuting on the VM
    /// side; this cooldown exists for callers that bind raw Clicked instead of a command.
    /// `Interactable` is a separate flag ANDed with "not cooling down" onto the wrapped
    /// Button.interactable, so an external SetInteractable(false) is never silently
    /// overwritten when the cooldown timer elapses (the old BaseUIButton bug, REWRITE_PLAN.md
    /// 1.2).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour
    {
        [SerializeField] private float cooldownSeconds;

        /// <summary>Optional: plays on an accepted click. Typically the ButtonPunch preset.</summary>
        [SerializeField] private UIMotion punchMotion;
        [SerializeField] private Button button;

        private readonly Subject<Unit> _clicked = new();
        private IUIClickFeedback[] _feedbackHooks;
        private bool _externallyInteractable = true;
        private float _cooldownUntilUnscaledTime = -1f;
        private bool _prepared;

        public Observable<Unit> Clicked
        {
            get
            {
                this.EnsurePrepared();
                return this._clicked;
            }
        }

        public Button Button => this.button ? this.button : this.button = this.GetComponent<Button>();

        /// <summary>External interactable flag, independent of the cooldown lock (both must
        /// be true for the wrapped Button to actually accept clicks).</summary>
        public bool Interactable
        {
            get => this._externallyInteractable;
            set
            {
                this._externallyInteractable = value;
                this.ApplyInteractable();
            }
        }

        private void Awake() => this.EnsurePrepared();

        /// <summary>
        /// Wires the click listener and caches IUIClickFeedback hooks. Not folded into Awake
        /// alone: AddComponent on a loose (non-prefab-instantiated) GameObject does not always
        /// run Awake synchronously in-Editor (confirmed empirically - see UIButtonTests, and
        /// the same class of gap PROGRESS.md documents for UIMotion/EditMode), so every public
        /// entry point below calls this first, same pattern as UIMotion.EnsurePrepared.
        /// </summary>
        private void EnsurePrepared()
        {
            if (this._prepared)
                return;

            this._prepared = true;
            this._feedbackHooks = this.GetComponents<IUIClickFeedback>();
            this.Button.onClick.AddListener(this.HandleClick);
        }

        private void OnDestroy()
        {
            if (this.button)
                this.button.onClick.RemoveListener(this.HandleClick);
            this._clicked.Dispose();
        }

        private void HandleClick()
        {
            if (this.IsCoolingDown())
                return;

            if (this.cooldownSeconds > 0f)
            {
                this._cooldownUntilUnscaledTime = Time.unscaledTime + this.cooldownSeconds;
                this.ApplyInteractable();
            }

            if (this.punchMotion)
                this.punchMotion.PlayShowAsync().Forget();

            foreach (IUIClickFeedback hook in this._feedbackHooks)
                hook.OnUIButtonClicked();

            this._clicked.OnNext(Unit.Default);
        }

        private bool IsCoolingDown() => this._cooldownUntilUnscaledTime >= 0f && Time.unscaledTime < this._cooldownUntilUnscaledTime;

        private void ApplyInteractable() => this.Button.interactable = this._externallyInteractable && !this.IsCoolingDown();

        /// <summary>Re-applies the cooldown-derived interactable state; call this from an
        /// external tick (e.g. a VM-driven timer) if you need the button to re-enable itself
        /// the instant cooldown elapses rather than on the next click attempt. Not required
        /// for correctness - IsCoolingDown is checked again on the next click regardless.</summary>
        public void RefreshInteractable() => this.ApplyInteractable();

        /// <summary>Test-only setup hook (see AssemblyInfo.cs InternalsVisibleTo): assigns
        /// cooldownSeconds directly instead of through the Inspector, and forces EnsurePrepared
        /// so a test can invoke Button.onClick directly without depending on Awake's timing.</summary>
        internal void ConfigureForTest(float cooldownInSeconds)
        {
            this.cooldownSeconds = cooldownInSeconds;
            this.EnsurePrepared();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.button)
                this.button = this.GetComponent<Button>();
        }
#endif
    }
}
