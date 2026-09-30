using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// Per-scope CanvasScaler overrides (e.g. a landscape scene in a portrait game). Applied when
    /// the scope registers and reverted when it disposes; overrides stack, the most recently
    /// registered scope wins. The layer list is deliberately not overridable: it is the game-wide
    /// contract that BackRouter and sort orders rely on.
    /// </summary>
    [CreateAssetMenu(fileName = "UIScopeOverrides", menuName = "DracoRuan/UISystem/Scope Overrides")]
    public sealed class UIScopeOverrides : ScriptableObject
    {
        [SerializeField] private bool overrideReferenceResolution;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private bool overrideMatch;
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

        public bool OverrideReferenceResolution => this.overrideReferenceResolution;
        public Vector2 ReferenceResolution => this.referenceResolution;
        public bool OverrideMatch => this.overrideMatch;
        public float MatchWidthOrHeight => this.matchWidthOrHeight;

        public Vector2 ResolveReferenceResolution(Vector2 fallback) =>
            this.overrideReferenceResolution ? this.referenceResolution : fallback;

        public float ResolveMatch(float fallback) => this.overrideMatch ? this.matchWidthOrHeight : fallback;
    }
}
