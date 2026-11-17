using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(MusicTrack)), CanEditMultipleObjects]
    public sealed class MusicTrackInspector : UnityEditor.Editor
    {
        private static void Heading(VisualElement root, string text)
        {
            Label heading = new Label(text);
            heading.AddToClassList("audiomw-profile-heading");
            root.Add(heading);
        }

        private void Field(VisualElement root, string path, string label)
        {
            root.Add(new PropertyField(serializedObject.FindProperty(path), label) { name = "track-" + path });
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement { name = "audiomw-track-inspector" };
            AudioProfileInspectorLayout.Configure(root);
            Heading(root, "Sources");
            Field(root, "introClip", "Intro clip");
            Field(root, "loopClip", "Body clip");
            Field(root, "loop", "Loop body");
            Label duration = new Label { name = "track-duration" }; root.Add(duration);
            root.Add(new Label("Intros play when starting from silence. Transitions enter the destination body."));
            Heading(root, "Musical grid");
            Field(root, "tempo", "Tempo (BPM)");
            Field(root, "beatsPerBar", "Beats per bar");
            Field(root, "markers", "Markers");
            root.Add(new Label("Marker bar and beat indices start at 0. The grid schedules playback; it does not time-stretch clips."));
            Heading(root, "Vertical layers");
            Field(root, "layers", "Layers");
            root.Add(new Label("A layer without a parameter uses its default weight. Parameter-driven layers normalize input before evaluating their weight curve. These are authored settings, not live meters."));
            Heading(root, "Output");
            Field(root, "mixerGroup", "Mixer output");
            DecibelGainField gain = new DecibelGainField("Gain") { name = "track-gain" };
            gain.BindProperty(serializedObject.FindProperty("volume")); root.Add(gain);
            HelpBox issues = new HelpBox("", HelpBoxMessageType.Warning) { name = "track-validation" }; root.Add(issues);
            System.Action refresh = () =>
            {
                List<string> warnings = new List<string>();
                foreach (var asset in targets)
                {
                    MusicTrack item = (MusicTrack)asset;
                    if (!item.HasContent) warnings.Add(item.name + ": assign an intro, body clip or playable layer.");
                    if (double.IsNaN(item.Tempo) || double.IsInfinity(item.Tempo) || item.Tempo < 1 || item.BeatsPerBar < 1)
                        warnings.Add(item.name + ": use finite tempo of at least 1 BPM and at least 1 beat per bar.");
                }
                var track = (MusicTrack)target;
                duration.text = targets.Length > 1 ? "Durations · Multiple tracks selected" : "Source durations · intro " + track.IntroDuration.ToString("0.###") + " s · body " + track.BodyDuration.ToString("0.###") + " s";
                issues.text = string.Join("\n", warnings);
                issues.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh()); refresh();
            root.Add(new Button(MusicStatePreviewWindow.OpenMenu) { text = "Open Music workspace", tooltip = "Select a state profile there to audition through the scene runtime. Opening the workspace starts no playback." });
            return root;
        }
    }
}
