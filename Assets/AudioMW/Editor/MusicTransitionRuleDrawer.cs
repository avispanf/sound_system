using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomPropertyDrawer(typeof(MusicTransitionRule))]
    public sealed class MusicTransitionRuleDrawer : PropertyDrawer
    {
        private static readonly string[] Fields = { "From", "To", "Quantization", "FadeSeconds", "Curve" };
        private static readonly string[] Labels = { "From state", "To state", "Boundary", "Crossfade (s)", "Curve" };

        private static string Title(SerializedProperty property)
        {
            return StateName(property.FindPropertyRelative("From")) + " → " + StateName(property.FindPropertyRelative("To"));
        }

        private static string StateName(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues) return "Mixed";
            return string.IsNullOrEmpty(property.stringValue) ? "Unassigned" : property.stringValue;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Foldout root = new Foldout { text = Title(property), value = property.isExpanded, name = "music-transition-rule" };
            root.RegisterValueChangedCallback(evt => { if (evt.target == root) property.isExpanded = evt.newValue; });
            for (int i = 0; i < Fields.Length; i++)
                root.Add(new PropertyField(property.FindPropertyRelative(Fields[i]), Labels[i]) { name = "music-rule-" + Fields[i] });
            root.TrackPropertyValue(property.FindPropertyRelative("From"), _ => root.text = Title(property));
            root.TrackPropertyValue(property.FindPropertyRelative("To"), _ => root.text = Title(property));
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (property.isExpanded ? 6 : 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, Title(property), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < Fields.Length; i++)
                {
                    row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(Fields[i]), new GUIContent(Labels[i]));
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
