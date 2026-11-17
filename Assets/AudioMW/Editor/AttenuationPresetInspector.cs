using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(AttenuationPreset)), CanEditMultipleObjects]
    public sealed class AttenuationPresetInspector : UnityEditor.Editor
    {
        private static void Heading(VisualElement root, string text)
        {
            Label heading = new Label(text);
            heading.AddToClassList("audiomw-profile-heading");
            root.Add(heading);
        }

        private PropertyField Field(VisualElement root, string path, string label)
        {
            var field = new PropertyField(serializedObject.FindProperty(path), label) { name = "attenuation-" + path };
            root.Add(field); return field;
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement { name = "audiomw-attenuation-inspector" };
            AudioProfileInspectorLayout.Configure(root, targets.Length == 1 ? target : null, serializedObject);
            Heading(root, "Distance response");
            Field(root, "minDistance", "Near distance (units)");
            Field(root, "maxDistance", "Far distance (units)");
            Label applied = new Label { name = "attenuation-applied-range" }; root.Add(applied);
            root.Add(new Label("Unity world units. Spatial preview assumes 1 unit = 1 metre. Applied near distance is at least 0.01; applied far distance is at least near. Stored values are not rewritten here."));
            Field(root, "rolloffMode", "Rolloff");
            PropertyField curve = Field(root, "customRolloff", "Custom response");
            Label curveNote = new Label { name = "attenuation-curve-status" }; root.Add(curveNote);
            Heading(root, "Spatial settings");
            Field(root, "spatialBlend", "3D blend");
            Field(root, "stereoPan", "Stereo pan");
            Field(root, "spread", "Spread (degrees)");
            Heading(root, "Distance processing");
            Field(root, "distanceFiltering", "Distance low-pass");
            var cutoff = Field(root, "cutoffByDistance", "Cutoff (Hz)");
            Field(root, "distanceSpread", "Distance spread");
            var spread = Field(root, "spreadByDistance", "Spread (degrees)");
            root.Add(new Label("Curve X: 0 = near, 1 = far. Cutoff uses Hz; spread uses degrees. Occlusion and distance filtering use the lower cutoff. Spread controls 3D stereo/multichannel extent, not mono-to-stereo conversion."));
            Field(root, "dopplerLevel", "Doppler level");
            root.Add(new Label("0 blend is 2D; 1 is fully 3D. Spread and Doppler are separate from distance attenuation. Reverb, occlusion and mixer gain are configured elsewhere."));
            System.Action refresh = () =>
            {
                var filterEnabled = serializedObject.FindProperty("distanceFiltering");
                var spreadEnabled = serializedObject.FindProperty("distanceSpread");
                cutoff.SetEnabled(filterEnabled.hasMultipleDifferentValues || filterEnabled.boolValue);
                spread.SetEnabled(spreadEnabled.hasMultipleDifferentValues || spreadEnabled.boolValue);
                var mode = serializedObject.FindProperty("rolloffMode");
                bool editable = mode.hasMultipleDifferentValues || mode.intValue == (int)AudioRolloffMode.Custom;
                curve.SetEnabled(editable);
                curveNote.text = mode.hasMultipleDifferentValues ? "Mixed rolloff modes. Editing the curve updates all selected presets." : editable ? "Custom curve is passed to the native AudioSource. Inspect the distance model in Spatial; this curve is not a live output meter." : "Custom curve is inactive for this rolloff mode. Its authored values are preserved.";
                var preset = (AttenuationPreset)target;
                applied.text = targets.Length == 1 ? "Applied range · " + preset.MinDistance.ToString("0.###") + " → " + preset.MaxDistance.ToString("0.###") + " units" : "Applied ranges · Multiple presets selected; each keeps its own limits.";
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh()); refresh();
            var open = new Button(() => { var window = AudioInspectionWindow.Open(AudioInspectionWindow.Workspace.Spatial); window.SelectAsset(target); }) { text = "Inspect distance response", name = "attenuation-workspace" };
            open.SetEnabled(targets.Length == 1 && EditorUtility.IsPersistent(target)); root.Add(open);
            return root;
        }
    }
}
