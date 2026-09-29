using System.Text.RegularExpressions;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.MotionTools
{
    /// <summary>
    /// One UIMotionTrack as a collapsible block: a single summary line when collapsed, and
    /// only the fields relevant to its `kind` (see UIMotionTrackFieldVisibility) when expanded,
    /// with typed value fields instead of the raw Vector4 the runtime stores. GetPropertyHeight
    /// and OnGUI share one Layout pass so the two can never disagree about which rows exist.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIMotionTrack))]
    public sealed class UIMotionTrackDrawer : PropertyDrawer
    {
        private static readonly Regex ArrayIndex = new Regex(@"\[(\d+)\]$", RegexOptions.Compiled);

        private static readonly string[] ModeNames =
        {
            "Absolute", "Relative To Rest", "Relative To Start", "Fraction Of Parent",
        };

        private sealed class Rows
        {
            private readonly Rect _origin;
            private readonly bool _draw;
            private float _y;

            public Rows(Rect origin, bool draw)
            {
                this._origin = origin;
                this._draw = draw;
            }

            public bool Draw => this._draw;

            public float Height => Mathf.Max(0f, this._y - EditorGUIUtility.standardVerticalSpacing);

            public Rect Next(float height)
            {
                var rect = new Rect(this._origin.x, this._origin.y + this._y, this._origin.width, height);
                this._y += height + EditorGUIUtility.standardVerticalSpacing;
                return rect;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            this.Layout(default, property, draw: false);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            this.Layout(position, property, draw: true);

        private float Layout(Rect position, SerializedProperty property, bool draw)
        {
            var rows = new Rows(position, draw);
            float line = EditorGUIUtility.singleLineHeight;

            SerializedProperty kindProp = property.FindPropertyRelative("kind");
            var kind = (UIMotionTrackKind)kindProp.intValue;
            var rectProperty = (UIMotionRectProperty)property.FindPropertyRelative("rectProperty").intValue;

            Rect header = rows.Next(line);
            if (draw)
                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, Summary(property, kind), true);

            if (!property.isExpanded)
                return rows.Height;

            EditorGUI.indentLevel++;

            this.Field(rows, property, "kind", "Kind");
            kind = (UIMotionTrackKind)kindProp.intValue;

            this.Field(rows, property, "target", UIMotionTrackFieldVisibility.TargetLabel(kind) + " (empty = this object)");
            this.TargetWarning(rows, property, kind);

            this.Field(rows, property, "startMode", "Start");
            this.Field(rows, property, "offset", "Offset");
            if (UIMotionTrackFieldVisibility.ShowDuration(kind))
                this.Field(rows, property, "duration", "Duration");

            if (UIMotionTrackFieldVisibility.ShowRectProperty(kind))
            {
                this.Field(rows, property, "rectProperty", "Rect Property");
                rectProperty = (UIMotionRectProperty)property.FindPropertyRelative("rectProperty").intValue;
            }

            if (UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(kind, rectProperty))
                this.Field(rows, property, "preserveVisualPosition", "Preserve Visual Position");

            this.ValueBlock(rows, property, kind, rectProperty);

            if (UIMotionTrackFieldVisibility.ShowEase(kind))
            {
                this.Field(rows, property, "useCurve", "Use Curve");
                if (property.FindPropertyRelative("useCurve").boolValue)
                    this.Field(rows, property, "curve", "Curve");
                else
                    this.Field(rows, property, "ease", "Ease");
            }

            if (UIMotionTrackFieldVisibility.ShowStagger(kind))
            {
                this.Field(rows, property, "stagger", "Stagger Children");
                if (property.FindPropertyRelative("stagger").boolValue)
                    this.Field(rows, property, "staggerDelay", "Stagger Delay");
            }

            if (UIMotionTrackFieldVisibility.ShowAnimatorFields(kind))
                this.AnimatorBlock(rows, property);

            EditorGUI.indentLevel--;
            return rows.Height;
        }

        private void Field(Rows rows, SerializedProperty parent, string name, string label)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            float height = EditorGUI.GetPropertyHeight(prop, true);
            Rect rect = rows.Next(height);
            if (rows.Draw)
                EditorGUI.PropertyField(rect, prop, new GUIContent(label), true);
        }

        private void ValueBlock(Rows rows, SerializedProperty property, UIMotionTrackKind kind, UIMotionRectProperty rectProperty)
        {
            float line = EditorGUIUtility.singleLineHeight;

            if (UIMotionTrackFieldVisibility.ShowUseStartValue(kind))
                this.Field(rows, property, "useStartValue", "Use Start Value");

            bool useStart = property.FindPropertyRelative("useStartValue").boolValue;

            if (UIMotionTrackFieldVisibility.ShowFromValue(kind, useStart))
            {
                this.ModePopup(rows, property, "fromValueMode", "Start Mode", kind, rectProperty, forTarget: false);
                this.ValueField(rows, property, "from", "Start Value",
                    UIMotionTrackFieldVisibility.FromShape(kind, rectProperty));
            }

            if (!UIMotionTrackFieldVisibility.ShowToValue(kind))
                return;

            if (UIMotionTrackFieldVisibility.ShowValueModes(kind))
                this.ModePopup(rows, property, "toValueMode", "Target Mode", kind, rectProperty, forTarget: true);

            this.ValueField(rows, property, "to", UIMotionTrackFieldVisibility.ToLabel(kind),
                UIMotionTrackFieldVisibility.ToShape(kind, rectProperty));

            if (kind == UIMotionTrackKind.Scale)
            {
                var mode = (UIMotionValueMode)property.FindPropertyRelative("toValueMode").intValue;
                if (mode is UIMotionValueMode.RelativeToRest or UIMotionValueMode.RelativeToStart)
                {
                    Rect hint = rows.Next(line);
                    if (rows.Draw)
                        EditorGUI.LabelField(hint, GUIContent.none, new GUIContent("Scale is a multiplier here, not an offset."), EditorStyles.miniLabel);
                }
            }
        }

        private void ModePopup(
            Rows rows, SerializedProperty property, string name, string label,
            UIMotionTrackKind kind, UIMotionRectProperty rectProperty, bool forTarget)
        {
            SerializedProperty prop = property.FindPropertyRelative(name);
            UIMotionValueMode[] allowed = UIMotionTrackFieldVisibility.AllowedModes(kind, rectProperty, forTarget);
            var current = (UIMotionValueMode)prop.intValue;

            int index = System.Array.IndexOf(allowed, current);
            bool orphan = index < 0;
            int count = allowed.Length + (orphan ? 1 : 0);

            var names = new string[count];
            for (int i = 0; i < allowed.Length; i++)
                names[i] = ModeNames[(int)allowed[i]];
            if (orphan)
            {
                // A mode left over from another kind: show it (so nothing is silently rewritten)
                // but flag it as not applicable.
                names[count - 1] = ModeNames[(int)current] + " (not applicable)";
                index = count - 1;
            }

            Rect rect = rows.Next(EditorGUIUtility.singleLineHeight);
            if (!rows.Draw)
                return;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(rect, label, index, names);
            if (EditorGUI.EndChangeCheck() && picked < allowed.Length)
                prop.intValue = (int)allowed[picked];
        }

        private void ValueField(Rows rows, SerializedProperty property, string name, string label, UIMotionValueShape shape)
        {
            Rect rect = rows.Next(EditorGUIUtility.singleLineHeight);
            if (!rows.Draw)
                return;

            SerializedProperty prop = property.FindPropertyRelative(name);
            Vector4 value = prop.vector4Value;

            EditorGUI.BeginChangeCheck();
            switch (shape)
            {
                case UIMotionValueShape.Toggle:
                    value.x = EditorGUI.Toggle(rect, label, value.x > 0.5f) ? 1f : 0f;
                    break;
                case UIMotionValueShape.Amplitude:
                case UIMotionValueShape.Float:
                    value.x = EditorGUI.FloatField(rect, label, value.x);
                    break;
                case UIMotionValueShape.Vector2:
                {
                    Vector2 v = EditorGUI.Vector2Field(rect, label, new Vector2(value.x, value.y));
                    value.x = v.x;
                    value.y = v.y;
                    break;
                }
                case UIMotionValueShape.Vector3:
                {
                    Vector3 v = EditorGUI.Vector3Field(rect, label, new Vector3(value.x, value.y, value.z));
                    value.x = v.x;
                    value.y = v.y;
                    value.z = v.z;
                    break;
                }
                case UIMotionValueShape.Color:
                {
                    Color c = EditorGUI.ColorField(rect, new GUIContent(label), new Color(value.x, value.y, value.z, value.w), true, true, false);
                    value = new Vector4(c.r, c.g, c.b, c.a);
                    break;
                }
                case UIMotionValueShape.Vector4:
                    value = EditorGUI.Vector4Field(rect, label, value);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
                prop.vector4Value = value;
        }

        private void AnimatorBlock(Rows rows, SerializedProperty property)
        {
            this.Field(rows, property, "animatorLayer", "Layer");

            SerializedProperty nameProp = property.FindPropertyRelative("animatorStateName");
            Rect nameRect = rows.Next(EditorGUI.GetPropertyHeight(nameProp, true));
            if (rows.Draw)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("State Name"));
                if (EditorGUI.EndChangeCheck())
                {
                    // Runtime plays and polls by hash, never by string (REWRITE_PLAN.md 2.7);
                    // keep the two in step so a typed name is never left with a stale hash.
                    property.FindPropertyRelative("animatorStateHash").intValue =
                        Animator.StringToHash(nameProp.stringValue);
                }
            }

            SerializedProperty hashProp = property.FindPropertyRelative("animatorStateHash");
            Rect hashRect = rows.Next(EditorGUIUtility.singleLineHeight);
            if (rows.Draw)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.IntField(hashRect, "State Hash", hashProp.intValue);
            }

            this.Field(rows, property, "animatorSpeed", "Speed");
        }

        private void TargetWarning(Rows rows, SerializedProperty property, UIMotionTrackKind kind)
        {
            if (!TryFindTargetProblem(property, kind, out string message, out GameObject owner, out System.Type addable))
                return;

            Rect box = rows.Next(EditorGUIUtility.singleLineHeight * 2f);
            if (rows.Draw)
                EditorGUI.HelpBox(EditorGUI.IndentedRect(box), message, MessageType.Warning);

            if (addable == null)
                return;

            Rect button = rows.Next(EditorGUIUtility.singleLineHeight);
            if (!rows.Draw || !GUI.Button(EditorGUI.IndentedRect(button), "Add " + addable.Name + " to " + owner.name))
                return;

            Undo.AddComponent(owner, addable);

            // The running preview resolved its targets before this component existed.
            if (property.serializedObject.targetObject is UIMotion motion)
                UIMotionPreviewController.NotifyEdited(motion);
        }

        private static bool HasTargetProblem(SerializedProperty property, UIMotionTrackKind kind) =>
            TryFindTargetProblem(property, kind, out _, out _, out _);

        /// <summary>The Inspector's answer to "why does nothing move?": a track whose target lacks
        /// the component its kind writes to is silently inert at runtime (a Fade with no CanvasGroup
        /// is the classic). Uses the same lookup as UIMotionTrackEvaluator: the object itself, else
        /// a component on its GameObject; an empty target means the UIMotion's own GameObject.</summary>
        private static bool TryFindTargetProblem(
            SerializedProperty property, UIMotionTrackKind kind,
            out string message, out GameObject owner, out System.Type addable)
        {
            message = null;
            addable = null;

            Object target = property.FindPropertyRelative("target").objectReferenceValue;
            owner = target is GameObject targetObject ? targetObject
                : target is Component component ? component.gameObject
                : property.serializedObject.targetObject is Component self ? self.gameObject
                : null;

            if (owner == null)
            {
                message = "This track has no target.";
                return true;
            }

            if (kind == UIMotionTrackKind.Custom)
            {
                if (target is IUIMotionCustomTrack || owner.TryGetComponent<IUIMotionCustomTrack>(out _))
                    return false;

                message = "'" + owner.name + "' has no component implementing IUIMotionCustomTrack, so this track does nothing.";
                return true;
            }

            System.Type[] accepted = UIMotionTrackFieldVisibility.RequiredComponents(kind);
            if (accepted.Length == 0 || System.Array.IndexOf(accepted, typeof(GameObject)) >= 0)
                return false;

            foreach (System.Type type in accepted)
            {
                if ((target != null && type.IsInstanceOfType(target)) || owner.TryGetComponent(type, out _))
                    return false;
            }

            var names = new string[accepted.Length];
            for (int i = 0; i < accepted.Length; i++)
                names[i] = accepted[i] == typeof(UnityEngine.UI.Graphic) ? "Graphic (Image, TextMeshPro...)" : accepted[i].Name;

            message = "'" + owner.name + "' has no " + string.Join(" or ", names) + ", so this " + kind + " track does nothing.";
            if (kind == UIMotionTrackKind.Fade)
                addable = typeof(CanvasGroup);
            return true;
        }

        private static string Summary(SerializedProperty property, UIMotionTrackKind kind)
        {
            Match match = ArrayIndex.Match(property.propertyPath);
            string index = match.Success ? "#" + match.Groups[1].Value + "  " : string.Empty;

            Object target = property.FindPropertyRelative("target").objectReferenceValue;
            string targetName = target != null ? target.name : "(this object)";

            var startMode = (UIMotionStartMode)property.FindPropertyRelative("startMode").intValue;
            float offset = property.FindPropertyRelative("offset").floatValue;
            float duration = property.FindPropertyRelative("duration").floatValue;

            string timing = StartModeName(startMode) + (Mathf.Approximately(offset, 0f) ? string.Empty : " " + offset.ToString("+0.##;-0.##") + "s");
            if (UIMotionTrackFieldVisibility.ShowDuration(kind))
                timing += "  " + duration.ToString("0.##") + "s";

            string flag = HasTargetProblem(property, kind) ? "(!) " : string.Empty;
            return flag + index + kind + "  ->  " + targetName + "     [" + timing + "]";
        }

        private static string StartModeName(UIMotionStartMode mode)
        {
            switch (mode)
            {
                case UIMotionStartMode.AfterPrevious:
                    return "After Previous";
                case UIMotionStartMode.AtTime:
                    return "At";
                default:
                    return "With Previous";
            }
        }
    }
}
