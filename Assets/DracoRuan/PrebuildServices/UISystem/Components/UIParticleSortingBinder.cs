using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Keeps a ParticleSystemRenderer sorted correctly against uGUI (REWRITE_PLAN.md 2.5:
    /// "gán sortingOrder của renderer = canvas.sortingOrder + offset, nằm trong khoảng
    /// sortStep"). `offset` must stay smaller than the owning layer's UILayerDefinition.SortStep
    /// so the particle never bleeds into the next view's sort order range - authoring-time
    /// concern, not enforced here (would need the layer definition, which this component
    /// doesn't reference to keep it usable outside the router too).
    /// </summary>
    public sealed class UIParticleSortingBinder : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private ParticleSystemRenderer particleRenderer;
        [SerializeField] private int offset;

        private void Reset()
        {
            this.canvas = this.GetComponentInParent<Canvas>();
            this.particleRenderer = this.GetComponent<ParticleSystemRenderer>();
        }

        private void OnEnable() => this.Apply();

        private void LateUpdate() => this.Apply();

        private void Apply()
        {
            if (!this.canvas || !this.particleRenderer)
                return;

            this.particleRenderer.sortingOrder = this.canvas.sortingOrder + this.offset;
        }

        /// <summary>Test-only setup hook (see AssemblyInfo.cs InternalsVisibleTo).</summary>
        internal void ConfigureForTest(Canvas targetCanvas, ParticleSystemRenderer targetRenderer, int sortOffset)
        {
            this.canvas = targetCanvas;
            this.particleRenderer = targetRenderer;
            this.offset = sortOffset;
            this.Apply();
        }
    }
}
