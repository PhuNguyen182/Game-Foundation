using System;
using R3;
using UnityEngine;
#if UISYSTEM_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// A row of tabs (REWRITE_PLAN.md 2.5/mục 5 bước 7): purely a view-side selector - which
    /// tab is selected is state owned by the screen's own VM (bound via UIBinder.TwoWay-style
    /// wiring, same as a Toggle group), not by UITabGroup itself. Each tab is a UIButton (for
    /// click) whose associated content GameObject this component shows/hides on selection.
    /// Gamepad LB/RB: SelectNext/SelectPrevious are the wiring point; AttachGamepadNavigation
    /// binds them to InputAction callbacks (mirrors InputSystemBackInputSource's own-or-borrow
    /// action pattern) when UISYSTEM_INPUT_SYSTEM is defined.
    /// </summary>
    public sealed class UITabGroup : MonoBehaviour
    {
        [Serializable]
        public struct Tab
        {
            public UIButton button;
            public GameObject content;
        }

        [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
        [SerializeField] private int initialIndex;

        private readonly ReactiveProperty<int> _selectedIndex = new ReactiveProperty<int>(-1);
        private DisposableBag _clickSubscriptions;
        private bool _prepared;

        /// <summary>Current selection, -1 if never selected (no tabs configured). Read-only
        /// from outside; drive it via Select/SelectNext/SelectPrevious, or bind a VM's own
        /// index property to those methods/this observable with UIBinder.</summary>
        public ReadOnlyReactiveProperty<int> SelectedIndex
        {
            get
            {
                this.EnsurePrepared();
                return this._selectedIndex;
            }
        }

        public int TabCount => this.tabs.Length;

        private void Awake() => this.EnsurePrepared();

        private void OnDestroy()
        {
            this._selectedIndex.Dispose();
            this._clickSubscriptions.Dispose();
#if UISYSTEM_INPUT_SYSTEM
            this.DetachGamepadNavigation();
#endif
        }

        private void EnsurePrepared()
        {
            if (this._prepared)
                return;

            this._prepared = true;

            this.WireClickHandlers();

            if (this.tabs.Length > 0)
                this.Select(Mathf.Clamp(this.initialIndex, 0, this.tabs.Length - 1));
        }

        private void WireClickHandlers()
        {
            this._clickSubscriptions.Clear();

            for (int i = 0; i < this.tabs.Length; i++)
            {
                int index = i;
                UIButton button = this.tabs[i].button;
                button?.Clicked.Subscribe(this, (_, self) => self.Select(index)).AddTo(ref this._clickSubscriptions);
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= this.tabs.Length)
                return;

            this._selectedIndex.Value = index;

            for (int i = 0; i < this.tabs.Length; i++)
            {
                GameObject content = this.tabs[i].content;
                if (content != null)
                    content.SetActive(i == index);
            }
        }

        public void SelectNext() => this.Select(this.WrapIndex(this._selectedIndex.Value + 1));

        public void SelectPrevious() => this.Select(this.WrapIndex(this._selectedIndex.Value - 1));

        private int WrapIndex(int index)
        {
            int count = this.tabs.Length;
            if (count == 0)
                return -1;

            return ((index % count) + count) % count;
        }

        /// <summary>Test-only setup hook (see AssemblyInfo.cs InternalsVisibleTo): assigns tabs
        /// directly instead of through the Inspector.</summary>
        internal void ConfigureForTest(Tab[] testTabs, int testInitialIndex = 0)
        {
            this.tabs = testTabs;
            this.initialIndex = testInitialIndex;
            this._prepared = false;
            this.EnsurePrepared();
        }

#if UISYSTEM_INPUT_SYSTEM
        private InputAction _previousTabAction;
        private InputAction _nextTabAction;
        private bool _ownsTabActions;

        /// <summary>
        /// Binds gamepad LB/RB (and, since these are ordinary InputActions, whatever else the
        /// bindings include) to SelectPrevious/SelectNext. Pass existing actions from the
        /// project's own action asset to share their enable/disable lifecycle with the rest of
        /// the game's input; omit either to fall back to a private action bound to the gamepad
        /// shoulder buttons directly, owned and disposed by this call's matching Detach.
        /// </summary>
        public void AttachGamepadNavigation(InputAction previousTabAction = null, InputAction nextTabAction = null)
        {
            this.DetachGamepadNavigation();

            if (previousTabAction != null && nextTabAction != null)
            {
                this._previousTabAction = previousTabAction;
                this._nextTabAction = nextTabAction;
                this._ownsTabActions = false;
            }
            else
            {
                this._previousTabAction = new InputAction("UITabGroup/Previous", InputActionType.Button, "<Gamepad>/leftShoulder");
                this._nextTabAction = new InputAction("UITabGroup/Next", InputActionType.Button, "<Gamepad>/rightShoulder");
                this._previousTabAction.Enable();
                this._nextTabAction.Enable();
                this._ownsTabActions = true;
            }

            this._previousTabAction.performed += this.OnPreviousTabPerformed;
            this._nextTabAction.performed += this.OnNextTabPerformed;
        }

        public void DetachGamepadNavigation()
        {
            if (this._previousTabAction != null)
                this._previousTabAction.performed -= this.OnPreviousTabPerformed;
            if (this._nextTabAction != null)
                this._nextTabAction.performed -= this.OnNextTabPerformed;

            if (this._ownsTabActions)
            {
                this._previousTabAction?.Dispose();
                this._nextTabAction?.Dispose();
            }

            this._previousTabAction = null;
            this._nextTabAction = null;
            this._ownsTabActions = false;
        }

        private void OnPreviousTabPerformed(InputAction.CallbackContext context) => this.SelectPrevious();

        private void OnNextTabPerformed(InputAction.CallbackContext context) => this.SelectNext();
#endif
    }
}
