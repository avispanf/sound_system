using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomPropertyDrawer(typeof(DecibelGainAttribute))]
    public sealed class DecibelGainDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            DecibelGainField field = new DecibelGainField(property.displayName);
            field.BindProperty(property);
            return field;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            Rect input = EditorGUI.PrefixLabel(position, new GUIContent(label.text + " (dB)", label.tooltip));
            Rect reset = new Rect(input.xMax - 38, input.y, 38, input.height);
            Rect number = new Rect(reset.x - 58, input.y, 54, input.height);
            Rect fader = new Rect(input.x, input.y, Mathf.Max(0, number.x - input.x - 4), input.height);
            if (GUI.Button(reset, new GUIContent("Reset", "Reset gain to 0 dB."))) property.floatValue = 1f;
            float db = Mathf.Clamp(AudioGain.ToDecibels(property.floatValue), DecibelGainField.FaderMinimumDb, 0f);
            EditorGUI.BeginChangeCheck();
            float next = GUI.HorizontalSlider(fader, db, DecibelGainField.FaderMinimumDb, 0f);
            if (EditorGUI.EndChangeCheck()) property.floatValue = AudioGain.FromDecibels(next);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.DelayedTextField(number, DecibelGainField.Format(property.floatValue));
            float linear;
            if (EditorGUI.EndChangeCheck() && DecibelGainField.TryParse(text, out linear)) property.floatValue = linear;
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
        }
    }
}
