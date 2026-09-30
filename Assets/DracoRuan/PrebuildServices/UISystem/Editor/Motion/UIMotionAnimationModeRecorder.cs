using System.Collections.Generic;
using System.Globalization;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.MotionTools
{
    /// <summary>
    /// Registers every property a previewing UIMotion is about to change with AnimationMode
    /// (REWRITE_PLAN.md 2.7: "mọi lệnh ghi giá trị đi qua một helper duy nhất, helper này đăng ký
    /// AddPropertyModification trước khi ghi"). Installed as UIMotionTrackEvaluator.PreWrite for
    /// the lifetime of a preview, so the first write to a property records its original value and
    /// AnimationMode.StopAnimationMode() puts it back - even if UIMotion.PreviewEnd never runs.
    /// A property this can't register (e.g. a TMP text color has no `m_Color`) is simply skipped;
    /// PreviewEnd's explicit rest-pose restore still covers it.
    /// </summary>
    internal static class UIMotionAnimationModeRecorder
    {
        private static readonly HashSet<(Object, string)> Registered = new HashSet<(Object, string)>();

        private static readonly string[] AnchoredPosition = { "m_AnchoredPosition.x", "m_AnchoredPosition.y" };
        private static readonly string[] LocalScale = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
        private static readonly string[] LocalRotation =
            { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        // Image and friends keep their tint in `m_Color`; TextMeshPro keeps it in `m_fontColor` and its
        // color setter never touches `m_Color`. A path the component does not have is skipped.
        private static readonly string[] GraphicColor =
        {
            "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a",
            "m_fontColor.r", "m_fontColor.g", "m_fontColor.b", "m_fontColor.a",
        };

        // Any Rect property can move any of these once anchors/pivot compensation is on.
        private static readonly string[] RectAll =
        {
            "m_AnchoredPosition.x", "m_AnchoredPosition.y", "m_SizeDelta.x", "m_SizeDelta.y",
            "m_AnchorMin.x", "m_AnchorMin.y", "m_AnchorMax.x", "m_AnchorMax.y", "m_Pivot.x", "m_Pivot.y",
        };

        public static void Install()
        {
            Registered.Clear();
            UIMotionTrackEvaluator.PreWrite = OnPreWrite;
        }

        public static void Uninstall()
        {
            UIMotionTrackEvaluator.PreWrite = null;
            Registered.Clear();
        }

        private static void OnPreWrite(UIMotionTrack track)
        {
            switch (track.kind)
            {
                case UIMotionTrackKind.Fade:
                    if (track.ResolvedFadeGraphic)
                        Register(track.ResolvedFadeGraphic, GraphicColor);
                    else
                        Register(track.ResolvedCanvasGroup, "m_Alpha");
                    break;
                case UIMotionTrackKind.Move:
                case UIMotionTrackKind.Shake:
                    Register(track.ResolvedRectTransform, AnchoredPosition);
                    break;
                case UIMotionTrackKind.Scale:
                case UIMotionTrackKind.Punch:
                    Register(track.ResolvedTransform, LocalScale);
                    break;
                case UIMotionTrackKind.Rotate:
                    Register(track.ResolvedTransform, LocalRotation);
                    break;
                case UIMotionTrackKind.Color:
                    Register(track.ResolvedGraphic, GraphicColor);
                    break;
                case UIMotionTrackKind.Fill:
                    Register(track.ResolvedImage, "m_FillAmount");
                    break;
                case UIMotionTrackKind.SetActive:
                    Register(track.ResolvedGameObject, "m_IsActive");
                    break;
                case UIMotionTrackKind.Rect:
                    Register(track.ResolvedRectTransform, RectAll);
                    break;
            }
        }

        private static void Register(Object target, params string[] propertyPaths)
        {
            if (!target || !AnimationMode.InAnimationMode())
                return;

            SerializedObject serialized = null;
            bool sampling = false;
            try
            {
                foreach (string path in propertyPaths)
                {
                    if (!Registered.Add((target, path)))
                        continue;

                    serialized ??= new SerializedObject(target);
                    SerializedProperty property = serialized.FindProperty(path);
                    string value = property == null ? null : ValueString(property);
                    if (value == null)
                        continue;

                    if (!sampling)
                    {
                        AnimationMode.BeginSampling();
                        sampling = true;
                    }

                    var modification = new PropertyModification
                    {
                        target = target,
                        propertyPath = path,
                        value = value,
                    };
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(HierarchyPath(target), target.GetType(), path);
                    AnimationMode.AddPropertyModification(binding, modification, false);
                }
            }
            finally
            {
                if (sampling)
                    AnimationMode.EndSampling();
                serialized?.Dispose();
            }
        }

        private static string ValueString(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "1" : "0";
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static string HierarchyPath(Object target)
        {
            Transform transform = target switch
            {
                Component component => component.transform,
                GameObject go => go.transform,
                _ => null,
            };

            if (!transform)
                return string.Empty;

            string path = transform.name;
            for (Transform parent = transform.parent; parent; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
