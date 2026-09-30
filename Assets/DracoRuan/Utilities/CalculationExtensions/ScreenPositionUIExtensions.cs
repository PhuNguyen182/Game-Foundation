using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DracoRuan.Utilities.CalculationExtensions
{
    /// <summary>
    /// Checks whether a screen position (mouse cursor / touch) is over a uGUI element.
    /// The <see cref="PointerEventData"/> and the result list are cached, so calls do not allocate after warm-up.
    /// Main thread only (shared static buffers).
    /// </summary>
    public static class ScreenPositionUIExtensions
    {
        private static readonly List<RaycastResult> CachedResults = new(16);
        private static PointerEventData cachedPointerData;
        private static EventSystem cachedPointerDataOwner;
        private static EventSystem eventSystem;

        /// <summary>
        /// True if a raycast-target uGUI element is under <paramref name="screenPosition"/>.
        /// UI on layers in <paramref name="ignoreLayers"/> is skipped; the default (nothing) counts every UI.
        /// </summary>
        public static bool IsOverUI(this Vector2 screenPosition, LayerMask ignoreLayers = default) =>
            screenPosition.IsOverUI(out _, ignoreLayers);

        /// <summary>
        /// True if a raycast-target uGUI element is under <paramref name="screenPosition"/>.
        /// UI on layers in <paramref name="ignoreLayers"/> is skipped; the default (nothing) counts every UI.
        /// <paramref name="topmost"/> is the front-most non-ignored hit object (null when nothing is hit).
        /// </summary>
        public static bool IsOverUI(this Vector2 screenPosition, out GameObject topmost,
            LayerMask ignoreLayers = default)
        {
            topmost = null;

            eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // PointerEventData keeps a reference to its EventSystem, so rebuild it only when the system changes.
            if (cachedPointerData == null || cachedPointerDataOwner != eventSystem)
            {
                cachedPointerData = new PointerEventData(eventSystem);
                cachedPointerDataOwner = eventSystem;
            }

            cachedPointerData.position = screenPosition;
            CachedResults.Clear();
            eventSystem.RaycastAll(cachedPointerData, CachedResults);

            // Results are sorted front-to-back, so the first non-ignored hit is the topmost.
            for (int i = 0; i < CachedResults.Count; i++)
            {
                GameObject hit = CachedResults[i].gameObject;
                if ((ignoreLayers.value & (1 << hit.layer)) != 0)
                {
                    continue;
                }

                topmost = hit;
                break;
            }

            CachedResults.Clear();
            return topmost != null;
        }

        /// <summary>Overload for <see cref="Vector3"/> positions (e.g. <c>Input.mousePosition</c>).</summary>
        public static bool IsOverUI(this Vector3 screenPosition, LayerMask ignoreLayers = default) =>
            ((Vector2)screenPosition).IsOverUI(out _, ignoreLayers);

        /// <summary>Overload for <see cref="Vector3"/> positions (e.g. <c>Input.mousePosition</c>).</summary>
        public static bool IsOverUI(this Vector3 screenPosition, out GameObject topmost,
            LayerMask ignoreLayers = default) =>
            ((Vector2)screenPosition).IsOverUI(out topmost, ignoreLayers);
    }
}
