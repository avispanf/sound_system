using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(MusicStateProfile))]
    public sealed class MusicStateProfileInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(new PropertyField(serializedObject.FindProperty("states"), "States"));
            root.Add(new PropertyField(serializedObject.FindProperty("defaultQuantization"), "Default transition"));
            root.Add(new PropertyField(serializedObject.FindProperty("transitions"), "Transition rules"));
            root.Add(new HelpBox("Names are case-sensitive. A route overrides the default timing. Transitions enter the destination body; intros play when starting from silence.", HelpBoxMessageType.Info));
            HelpBox message = new HelpBox("", HelpBoxMessageType.Warning);
            root.Add(message);
            System.Action refresh = () =>
            {
                message.text = ((MusicStateProfile)target).Validate();
                message.style.display = string.IsNullOrEmpty(message.text) ? DisplayStyle.None : DisplayStyle.Flex;
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh());
            refresh();
            root.Add(new Button(() => MusicStatePreviewWindow.Open((MusicStateProfile)target)) { text = "Open transition preview" });
            return root;
        }
    }
}
