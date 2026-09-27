using DracoRuan.PrebuildServices.UISystem.Logic.Badge;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// The UI-facing adapter for IBadgeService (REWRITE_PLAN.md mục 5 bước 10): shows/hides a
    /// red-dot Graphic and optionally a count label for one BadgeKey. Deliberately thin - all
    /// the actual counting/roll-up logic lives in IBadgeService (engine-agnostic, in `Logic/`);
    /// this component only listens for Changed on the key it cares about and refreshes its
    /// visuals, which is exactly the "adapter at the UI boundary" shape the plan uses elsewhere
    /// (see IUITextLocalizer).
    /// </summary>
    public sealed class UIBadgeIndicator : MonoBehaviour
    {
        [SerializeField] private GameObject dot;
        [SerializeField] private Text countLabelLegacy;
        [SerializeField] private TMPro.TMP_Text countLabel;
        [SerializeField] private int maxDisplayCount = 99;

        private IBadgeService _service;
        private BadgeKey _key;
        private bool _bound;

        /// <summary>Wires this indicator to `service`/`key` - called once by whatever composition
        /// root/binder owns the IBadgeService instance (VContainer-resolved, per the project's DI
        /// convention), since this component has no way to resolve it on its own.</summary>
        public void Bind(IBadgeService service, BadgeKey key)
        {
            this.Unbind();

            this._service = service;
            this._key = key;
            this._service.Changed += this.OnBadgeChanged;
            this._bound = true;

            this.Refresh();
        }

        public void Unbind()
        {
            if (!this._bound)
                return;

            this._service.Changed -= this.OnBadgeChanged;
            this._service = null;
            this._bound = false;
        }

        private void OnDestroy() => this.Unbind();

        private void OnBadgeChanged(BadgeKey changedKey)
        {
            if (this._key.IsAncestorOrSelfOf(changedKey) || changedKey.IsAncestorOrSelfOf(this._key))
                this.Refresh();
        }

        private void Refresh()
        {
            int count = this._service.GetCount(this._key);
            bool visible = count > 0;

            if (this.dot != null)
                this.dot.SetActive(visible);

            string text = count > this.maxDisplayCount ? $"{this.maxDisplayCount}+" : count.ToString();

            if (this.countLabel != null)
            {
                this.countLabel.SetText(text);
                this.countLabel.gameObject.SetActive(visible);
            }

            if (this.countLabelLegacy != null)
            {
                this.countLabelLegacy.text = text;
                this.countLabelLegacy.gameObject.SetActive(visible);
            }
        }
    }
}
