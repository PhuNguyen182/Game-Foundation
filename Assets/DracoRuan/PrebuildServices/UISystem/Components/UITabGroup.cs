using System;
using DracoRuan.PrebuildServices.UISystem.Input;
using R3;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// A row of tabs (REWRITE_PLAN.md 2.5/mục 5 bước 7): purely a view-side selector - which
    /// tab is selected is state owned by the screen's own VM (bound via UIBinder.TwoWay-style
    /// wiring, same as a Toggle group), not by UITabGroup itself. Each tab is a UIButton (for
    /// click) whose associated content GameObject this component shows/hides on selection.
    /// Gamepad LB/RB: SelectNext/SelectPrevious are the wiring point; AttachNavigation
    /// subscribes them to any IUITabNavigationSource (InputSystemTabNavigationSource ships in
    /// the UISystem.InputSystem adapter assembly).
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

        private readonly ReactiveProperty<int> _selectedIndex = new(-1);
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
this.DetachNavigation();
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
                if (content)
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

        private IUITabNavigationSource _navigationSource;

        /// <summary>Subscribes SelectPrevious/SelectNext to the source. The source is borrowed:
        /// the caller keeps ownership and disposes it.</summary>
        public void AttachNavigation(IUITabNavigationSource source)
        {
            this.DetachNavigation();

            if (source == null)
                return;

            this._navigationSource = source;
            source.Previous += this.SelectPrevious;
            source.Next += this.SelectNext;
        }

        public void DetachNavigation()
        {
            if (this._navigationSource == null)
                return;

            this._navigationSource.Previous -= this.SelectPrevious;
            this._navigationSource.Next -= this.SelectNext;
            this._navigationSource = null;
        }
    }
}
