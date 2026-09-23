using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>
    /// Draws a <see cref="VibrationTimeline"/> as a strip of bars, since Play never vibrates in the
    /// Editor (MOST compiles its native calls out under <c>!UNITY_EDITOR</c>) and this is the only
    /// feedback authoring a pattern or curve gets without a device.
    /// </summary>
    public static class VibrationTimelineView
    {
        private const float Height = 48f;
        private static readonly Color BarColor = new Color(0.3f, 0.65f, 0.95f, 1f);
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.08f);
        private static readonly Color MarkerColor = new Color(0.95f, 0.55f, 0.2f, 1f);

        public static void Draw(in VibrationTimeline timeline)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(Height));

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, BackgroundColor);

            if (timeline.Segments.Count == 0)
            {
                GUI.Label(rect, "Nothing authored yet.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            float totalMs = Mathf.Max(1f, timeline.TotalMs);

            for (int i = 0; i < timeline.Segments.Count; i++)
            {
                VibrationTimelineSegment segment = timeline.Segments[i];

                float x = rect.x + rect.width * (segment.StartMs / totalMs);
                float width = segment.DurationMs > 0f
                    ? Mathf.Max(2f, rect.width * (segment.DurationMs / totalMs))
                    : 2f;

                float barHeight = Mathf.Max(2f, rect.height * segment.Strength01);
                Rect barRect = new Rect(x, rect.yMax - barHeight, width, barHeight);

                EditorGUI.DrawRect(barRect, segment.DurationMs > 0f ? BarColor : MarkerColor);
            }

            GUI.Label(
                new Rect(rect.x, rect.y, rect.width - 4f, 16f),
                $"{timeline.TotalMs:0} ms",
                EditorStyles.miniLabel);
        }
    }
}
