using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomPropertyDrawer(typeof(MixerParameterBinding))]
    public sealed class MixerParameterBindingDrawer : PropertyDrawer
    {
        private static readonly string[] Fields = { "parameter", "exposedName", "curve", "outputMin", "outputMax" };
        private static readonly string[] Labels = { "Sound parameter", "Exposed name", "Response curve", "Output minimum", "Output maximum" };

        private static string Title(SerializedProperty property)
        {
            SerializedProperty source = property.FindPropertyRelative("parameter");
            SerializedProperty destination = property.FindPropertyRelative("exposedName");
            string from = source.hasMultipleDifferentValues ? "Mixed parameter" : source.objectReferenceValue != null ? source.objectReferenceValue.name : "Unassigned parameter";
            string to = destination.hasMultipleDifferentValues ? "Mixed target" : string.IsNullOrEmpty(destination.stringValue) ? "Unassigned target" : destination.stringValue;
            return from + " → " + to;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Foldout root = new Foldout { text = Title(property), value = property.isExpanded, name = "mixer-parameter-binding" };
            root.RegisterValueChangedCallback(evt => { if (evt.target == root) property.isExpanded = evt.newValue; });
            for (int i = 0; i < Fields.Length; i++)
            {
                BindableElement field;
                if (Fields[i] == "parameter") field = new ObjectField(Labels[i]) { objectType = typeof(SoundParameter), allowSceneObjects = false };
                else if (Fields[i] == "exposedName") field = new TextField(Labels[i]);
                else if (Fields[i] == "curve") field = new CurveField(Labels[i]);
                else field = new FloatField(Labels[i]);
                field.bindingPath = property.FindPropertyRelative(Fields[i]).propertyPath;
                var row = new VisualElement { name = "mixer-binding-" + Fields[i] }; row.Add(field); root.Add(row);
            }
            root.contentContainer.TrackSerializedObjectValue(property.serializedObject, _ => root.text = Title(property));
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
