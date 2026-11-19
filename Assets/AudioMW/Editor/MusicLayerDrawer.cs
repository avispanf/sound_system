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
            {
                BindableElement field;
                switch (Fields[i])
                {
                    case "layerName": field = new TextField(Labels[i]); break;
                    case "clip": field = new ObjectField(Labels[i]) { objectType = typeof(AudioClip), allowSceneObjects = false }; break;
                    case "parameter": field = new ObjectField(Labels[i]) { objectType = typeof(SoundParameter), allowSceneObjects = false }; break;
                    case "weightCurve": field = new CurveField(Labels[i]); break;
                    case "defaultWeight": field = new Slider(Labels[i],0,1) { showInputField = true }; break;
                    default: field = new FloatField(Labels[i]); break;
                }
                field.bindingPath = property.FindPropertyRelative(Fields[i]).propertyPath;
                var row = new VisualElement { name = "music-layer-" + Fields[i] }; row.Add(field); root.Add(row);
            }
            void RefreshTitle()
            {
                root.text = Title(property);
                root.tooltip = root.text;
                var name = property.FindPropertyRelative("layerName");
                root.Q<TextField>().tooltip = name.hasMultipleDifferentValues ? "Mixed layer names" : name.stringValue;
                foreach (var field in root.Query<ObjectField>().ToList())
                {
                    var reference = property.serializedObject.FindProperty(field.bindingPath);
                    field.tooltip = reference.hasMultipleDifferentValues ? "Mixed references" : AssetDatabase.GetAssetPath(reference.objectReferenceValue);
                }
            }
            RefreshTitle();
            root.contentContainer.TrackSerializedObjectValue(property.serializedObject, _ => RefreshTitle());
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
