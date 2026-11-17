using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomPropertyDrawer(typeof(MusicLayer))]
    public sealed class MusicLayerDrawer : PropertyDrawer
    {
        private static readonly string[] Fields = { "layerName", "clip", "parameter", "weightCurve", "defaultWeight", "fadeSeconds" };
        private static readonly string[] Labels = { "Layer name", "Source clip", "Sound parameter", "Weight curve", "Fallback weight", "Fade (seconds)" };

        private static string Title(SerializedProperty property)
        {
            SerializedProperty name = property.FindPropertyRelative("layerName");
            SerializedProperty parameter = property.FindPropertyRelative("parameter");
            string title = name.hasMultipleDifferentValues ? "Mixed layers" : string.IsNullOrEmpty(name.stringValue) ? "Unnamed layer" : name.stringValue;
            string control = parameter.hasMultipleDifferentValues ? "Mixed parameter" : parameter.objectReferenceValue != null ? parameter.objectReferenceValue.name : "Fixed weight";
            return title + " · " + control;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Foldout root = new Foldout { text = Title(property), value = property.isExpanded, name = "music-layer" };
            root.RegisterValueChangedCallback(evt => { if (evt.target == root) property.isExpanded = evt.newValue; });
            for (int i = 0; i < Fields.Length; i++)
                root.Add(new PropertyField(property.FindPropertyRelative(Fields[i]), Labels[i]) { name = "music-layer-" + Fields[i] });
            root.TrackPropertyValue(property.FindPropertyRelative("parameter"), _ => root.text = Title(property));
            root.TrackPropertyValue(property.FindPropertyRelative("layerName"), _ => root.text = Title(property));
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (property.isExpanded ? 7 : 1);
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
