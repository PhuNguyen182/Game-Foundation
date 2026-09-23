using System;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawers
{
    /// <summary>
    /// Shared drawing for the vibration id dropdown.
    /// </summary>
    /// <remarks>
    /// <para><b>A missing value is shown, never silently reset.</b> If an entry was renamed or
    /// deleted, the field keeps the value it had and says so. Clearing it automatically would erase
    /// the only evidence of what the field used to point at, in a file nobody is looking at. Mirrors
    /// <c>AudioIdDropdownDrawer</c>.</para>
    ///
    /// <para>Repaint cost is one integer comparison: the arrays are rebuilt only when
    /// <see cref="VibrationIdIndex.Version"/> changes, not per frame.</para>
    /// </remarks>
    public abstract class VibrationIdDropdownDrawer : PropertyDrawer
    {
        private const float HelpBoxHeight = 30f;
        private const float PingButtonWidth = 22f;

        private string[] _ids = Array.Empty<string>();
        private string[] _labels = Array.Empty<string>();
        private int _cachedVersion = -1;

        /// <summary>The ids this drawer offers.</summary>
        protected abstract string[] SourceIds { get; }

        /// <summary>Display labels, parallel to <see cref="SourceIds"/>.</summary>
        protected abstract string[] SourceLabels { get; }

        /// <summary>Whether an empty value is a legitimate choice.</summary>
        protected abstract bool AllowEmpty { get; }

        /// <summary>What to say when the stored value matches nothing.</summary>
        protected abstract string MissingHint { get; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return HelpBoxHeight;

            this.RefreshCache();

            return this.IsMissing(property.stringValue)
                ? EditorGUIUtility.singleLineHeight + 2f + HelpBoxHeight
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, $"[{this.GetType().Name}] can only decorate a string field.",
                    MessageType.Error);
                return;
            }

            this.RefreshCache();

            // BeginProperty gives prefab-override bolding, right-click Revert and multi-edit
            // handling for free, and looks broken without it.
            label = EditorGUI.BeginProperty(position, label, property);

            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string current = property.stringValue;
            bool missing = this.IsMissing(current);

            string[] options = this.BuildOptions(current, missing, out int selectedIndex);

            Rect popupRect = new Rect(row.x, row.y, row.width - PingButtonWidth - 2f, row.height);
            Rect pingRect = new Rect(row.xMax - PingButtonWidth, row.y, PingButtonWidth, row.height);

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(popupRect, label.text, selectedIndex, options);

            if (EditorGUI.EndChangeCheck() && picked != selectedIndex)
            {
                // The synthetic "missing" option is never written back, so the stored value survives
                // until a real replacement is chosen.
                string chosen = this.ValueForOption(picked, current, missing);
                if (chosen != null)
                    property.stringValue = chosen;
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(current) || missing))
            {
                if (GUI.Button(pingRect, new GUIContent("◎", "Show the owning asset in the Project window"),
                        EditorStyles.miniButton))
                    PingOwner(current);
            }

            if (missing)
            {
                Rect helpRect = new Rect(position.x, row.yMax + 2f, position.width, HelpBoxHeight);
                EditorGUI.HelpBox(helpRect, string.Format(this.MissingHint, current), MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private void RefreshCache()
        {
            if (this._cachedVersion == VibrationIdIndex.Version)
                return;

            this._ids = this.SourceIds;
            this._labels = this.SourceLabels;
            this._cachedVersion = VibrationIdIndex.Version;
        }

        private bool IsMissing(string value) =>
            !string.IsNullOrEmpty(value) && Array.IndexOf(this._ids, value) < 0;

        private string[] BuildOptions(string current, bool missing, out int selectedIndex)
        {
            int extra = (this.AllowEmpty ? 1 : 0) + (missing ? 1 : 0);
            string[] options = new string[this._ids.Length + extra];

            int cursor = 0;
            if (this.AllowEmpty)
                options[cursor++] = "(none)";

            for (int i = 0; i < this._labels.Length; i++)
                options[cursor + i] = this._labels[i];

            cursor += this._labels.Length;

            if (missing)
                options[cursor] = $"⚠ {current}  (missing)";

            selectedIndex = 0;

            if (missing)
                selectedIndex = options.Length - 1;
            else if (!string.IsNullOrEmpty(current))
            {
                int found = Array.IndexOf(this._ids, current);
                selectedIndex = found < 0 ? 0 : found + (this.AllowEmpty ? 1 : 0);
            }

            return options;
        }

        private string ValueForOption(int optionIndex, string current, bool missing)
        {
            if (this.AllowEmpty && optionIndex == 0)
                return string.Empty;

            int idIndex = optionIndex - (this.AllowEmpty ? 1 : 0);

            if (idIndex >= 0 && idIndex < this._ids.Length)
                return this._ids[idIndex];

            // The synthetic missing row: leave the value alone.
            return missing ? null : current;
        }

        private static void PingOwner(string id)
        {
            if (!VibrationIdIndex.TryGetOwnerPath(id, out string path))
                return;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }
    }
}
