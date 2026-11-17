using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(MixerRoutingProfile)), CanEditMultipleObjects]
    public sealed class MixerRoutingProfileInspector : UnityEditor.Editor
    {
        private static void Heading(VisualElement root, string text)
        {
            Label heading = new Label(text);
            heading.AddToClassList("audiomw-profile-heading");
            root.Add(heading);
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement { name = "audiomw-routing-inspector" };
            AudioProfileInspectorLayout.Configure(root, targets.Length == 1 ? target : null);
            Heading(root, "Target mixer");
            root.Add(new PropertyField(serializedObject.FindProperty("mixer"), "AudioMixer") { name = "routing-mixer" });
            Button open = new Button(() => AssetDatabase.OpenAsset(((MixerRoutingProfile)target).Mixer)) { text = "Open native AudioMixer", name = "routing-open-mixer" };
            root.Add(open);
            Heading(root, "Parameter bindings");
            root.Add(new Label("Sound parameter → response curve → exposed mixer parameter") { name = "routing-flow" });
            root.Add(new PropertyField(serializedObject.FindProperty("bindings"), "Bindings") { name = "routing-bindings" });
            root.Add(new HelpBox("Output min/max use the target mixer parameter's units: dB for volume, Hz for frequency, or the effect's own units. Input is normalized to 0–1 before the curve; its result is clamped to 0–1 before mapping to the output range.", HelpBoxMessageType.Info));
            Heading(root, "Exposed parameter checks");
            root.Add(new PropertyField(serializedObject.FindProperty("expectedExposedNames"), "Expected names") { name = "routing-expected-names" });
            root.Add(new Label("Expected names are additional checks, not bindings. Names must match the native AudioMixer exactly."));
            HelpBox validation = new HelpBox("", HelpBoxMessageType.Warning) { name = "routing-profile-validation" };
            root.Add(validation);
            System.Action refresh = () =>
            {
                List<string> issues = new List<string>();
                foreach (var asset in targets)
                {
                    MixerRoutingProfile profile = (MixerRoutingProfile)asset;
                    string reason = DescribeIssues(profile);
                    if (!string.IsNullOrEmpty(reason)) issues.Add(targets.Length > 1 ? profile.name + ": " + reason : reason);
                }
                validation.text = string.Join("\n", issues);
                validation.style.display = issues.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                open.SetEnabled(targets.Length == 1 && ((MixerRoutingProfile)target).Mixer != null);
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh());
            root.Add(new Button(() => refresh()) { text = "Check exposed names", name = "routing-check" });
            root.Add(new Label("Checks read the assigned mixer. They do not write values or prove that this profile is registered with the runtime Mixer Director."));
            refresh();
            return root;
        }

        public static string DescribeIssues(MixerRoutingProfile profile)
        {
            List<string> issues = new List<string>();
            if (profile.Mixer == null) issues.Add("Assign an AudioMixer. Exposed parameter checks are unavailable until then.");
            if (profile.Bindings.Length == 0) issues.Add("Add a binding to drive a mixer parameter.");
            for (int i = 0; i < profile.Bindings.Length; i++)
                if (profile.Bindings[i] == null || !profile.Bindings[i].IsValid)
                    issues.Add("Binding " + (i + 1) + " needs a Sound Parameter, an exposed name and a non-empty curve.");
            if (profile.Mixer != null)
                foreach (string name in profile.FindMissingExposedParameters()) issues.Add("Exposed parameter not found: " + name);
            return string.Join("\n", issues);
        }
    }
}
