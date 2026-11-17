using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(MusicStateProfile))]
    public sealed class MusicStateProfileInspector : UnityEditor.Editor
    {
        private static void Heading(VisualElement root, string text)
        {
            Label heading = new Label(text);
            heading.AddToClassList("audiomw-profile-heading");
            root.Add(heading);
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement { name = "audiomw-music-inspector" };
            AudioWorkspace.Style(root);
            root.AddToClassList("audiomw-inspection");
            root.RegisterCallback<GeometryChangedEvent>(evt => root.EnableInClassList("audiomw-inspector-narrow", evt.newRect.width < 380));
            Label summary = new Label { name = "music-profile-summary" };
            summary.AddToClassList("audiomw-subtitle");
            root.Add(summary);
            Heading(root, "States");
            root.Add(new PropertyField(serializedObject.FindProperty("states"), "States"));
            Heading(root, "Default transition");
            root.Add(new Label("Used when no matching transition rule exists.") { style = { whiteSpace = WhiteSpace.Normal } });
            root.Add(new PropertyField(serializedObject.FindProperty("defaultQuantization"), "Boundary"));
            root.Add(new PropertyField(serializedObject.FindProperty("defaultFadeSeconds"), "Crossfade (s)"));
            root.Add(new PropertyField(serializedObject.FindProperty("defaultCurve"), "Curve"));
            Heading(root, "Transition rules");
            root.Add(new PropertyField(serializedObject.FindProperty("transitions"), "Routes"));
            root.Add(new HelpBox("Names are case-sensitive. A route overrides timing, crossfade duration and curve. Zero seconds makes a cut. Transitions enter the destination body; intros play when starting from silence.", HelpBoxMessageType.Info));
            HelpBox message = new HelpBox("", HelpBoxMessageType.Warning) { name = "music-profile-validation" };
            root.Add(message);
            System.Action refresh = () =>
            {
                MusicStateProfile profile = (MusicStateProfile)target;
                summary.text = profile.States.Length + " states · " + profile.Transitions.Length + " transition rules";
                message.text = profile.Validate();
                message.style.display = string.IsNullOrEmpty(message.text) ? DisplayStyle.None : DisplayStyle.Flex;
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh());
            refresh();
            Heading(root, "Runtime preview");
            root.Add(new Label("Preview uses a Music Player in the current scene during Play Mode. Opening the workspace does not start playback.") { style = { whiteSpace = WhiteSpace.Normal } });
            root.Add(new Button(() => MusicStatePreviewWindow.Open((MusicStateProfile)target)) { text = "Open Music workspace", name = "music-profile-preview" });
            return root;
        }
    }
}
