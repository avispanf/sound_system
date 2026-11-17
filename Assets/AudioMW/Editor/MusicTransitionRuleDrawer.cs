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
            {
                BindableElement field;
                if (Fields[i] == "From" || Fields[i] == "To") field = new TextField(Labels[i]);
                else if (Fields[i] == "Quantization") field = new EnumField(Labels[i], MusicQuantization.Bar);
                else if (Fields[i] == "Curve") field = new EnumField(Labels[i], default(MusicFadeCurve));
                else
                {
                    var fade = new FloatField(Labels[i]);
                    fade.RegisterValueChangedCallback(evt => { if (evt.newValue < 0f) fade.value = 0f; });
                    field = fade;
                }
                field.bindingPath = property.FindPropertyRelative(Fields[i]).propertyPath;
                var row = new VisualElement { name = "music-rule-" + Fields[i] }; row.Add(field); root.Add(row);
            }
            var targets = property.serializedObject.targetObjects;
            string path = property.propertyPath;
            System.Action refreshTitle = () =>
            {
                foreach (var target in targets) if (target == null) return;
                using (var current = new SerializedObject(targets))
                {
                    var route = current.FindProperty(path);
                    if (route != null) root.text = Title(route);
                }
            };
            root.contentContainer.TrackSerializedObjectValue(property.serializedObject, _ => refreshTitle());
            root.RegisterCallback<ChangeEvent<string>>(_ => root.schedule.Execute(refreshTitle));
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
