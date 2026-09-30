using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Registry
{
    /// <summary>Which UIViewDefinition fields matter for which preset, so the registry tool can
    /// hide the rest. Mirrors how UIService reads each flag (Screen stack flags only apply to
    /// Screen; modal/backdrop to popups; visibleOnScreens to Overlay HUDs).</summary>
    public static class UIViewDefinitionFieldVisibility
    {
        /// <summary>Draw order for every field the tool can show.</summary>
        public static readonly string[] AllFields =
        {
            "viewModelType", "viewPrefab", "addressablePrefab", "layer", "preset",
            "cachePolicy", "preload", "hideMode", "reopenPolicy",
            "modal", "closeOnBackdrop", "scope",
            "hidesBelow", "history", "participatesInStack",
            "visibleOnScreens", "defaultPriority", "speedOverride",
        };

        public static bool IsVisible(string field, UIViewPreset preset, bool showAll)
        {
            switch (field)
            {
                case "viewModelType":
                case "viewPrefab":
                case "addressablePrefab":
                case "layer":
                case "preset":
                    return true;
            }

            if (showAll)
                return true;

            switch (field)
            {
                case "preload":
                    return preset != UIViewPreset.Overlay;
                case "hidesBelow":
                case "history":
                case "participatesInStack":
                    return preset == UIViewPreset.Screen;
                case "modal":
                case "closeOnBackdrop":
                    return preset == UIViewPreset.Popup;
                case "scope":
                    return preset != UIViewPreset.Screen;
                case "visibleOnScreens":
                    return preset == UIViewPreset.Overlay;
                default:
                    return true;
            }
        }

        /// <summary>Writes the preset's default flag values into the serialized definition
        /// (does not apply; the caller owns ApplyModifiedProperties).</summary>
        public static void ApplyPresetDefaults(SerializedObject definition, UIViewPreset preset)
        {
            bool screen = preset == UIViewPreset.Screen;
            bool popup = preset == UIViewPreset.Popup;

            SetBool(definition, "hidesBelow", screen);
            SetBool(definition, "history", screen);
            SetBool(definition, "participatesInStack", screen);
            SetBool(definition, "modal", popup || preset == UIViewPreset.System);
            SetBool(definition, "closeOnBackdrop", popup);

            SetEnum(definition, "scope", screen || popup || preset == UIViewPreset.Tutorial
                ? (int)UIViewScope.Screen
                : (int)UIViewScope.Global);

            SetEnum(definition, "reopenPolicy", preset == UIViewPreset.Toast
                ? (int)UIReopenPolicy.AllowMultiple
                : (int)UIReopenPolicy.BringToFront);
        }

        private static void SetBool(SerializedObject definition, string field, bool value)
        {
            SerializedProperty property = definition.FindProperty(field);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetEnum(SerializedObject definition, string field, int value)
        {
            SerializedProperty property = definition.FindProperty(field);
            if (property != null)
                property.enumValueIndex = value;
        }
    }
}
