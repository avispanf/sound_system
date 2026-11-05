using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomPropertyDrawer(typeof(SoundEventChild))]
    public sealed class SoundEventChildDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement { name = "child-event-row" };
            root.style.marginBottom = 8;
            root.Add(new PropertyField(property.FindPropertyRelative("soundEvent"), "Event") { name = "child-event" });
            root.Add(new PropertyField(property.FindPropertyRelative("label"), "Label") { tooltip = "Optional authoring label for this child." });
            PropertyField state = new PropertyField(property.FindPropertyRelative("state"), "State") { name = "child-state", tooltip = "Matches the rounded switch parameter when playback starts." };
            root.Add(state);
            DecibelGainField gain = new DecibelGainField("Gain") { name = "child-gain" };
            gain.BindProperty(property.FindPropertyRelative("gain"));
            root.Add(gain);
            System.Action refresh = () =>
            {
                SerializedProperty mode = property.serializedObject.FindProperty("containerMode");
                state.style.display = mode == null || mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)ContainerMode.Switch ? DisplayStyle.Flex : DisplayStyle.None;
            };
            root.TrackSerializedObjectValue(property.serializedObject, _ => refresh());
            refresh();
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (ShowState(property) ? 4 : 3);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Draw(ref row, property.FindPropertyRelative("soundEvent"), "Event");
            Draw(ref row, property.FindPropertyRelative("label"), "Label");
            if (ShowState(property)) Draw(ref row, property.FindPropertyRelative("state"), "State");
            Draw(ref row, property.FindPropertyRelative("gain"), "Gain");
            EditorGUI.EndProperty();
        }

        private static bool ShowState(SerializedProperty property)
        {
            SerializedProperty mode = property.serializedObject.FindProperty("containerMode");
            return mode == null || mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)ContainerMode.Switch;
        }

        private static void Draw(ref Rect row, SerializedProperty property, string label)
        {
            EditorGUI.PropertyField(row, property, new GUIContent(label));
            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
