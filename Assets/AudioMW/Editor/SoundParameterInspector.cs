using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(SoundParameter)), CanEditMultipleObjects]
    public sealed class SoundParameterInspector : UnityEditor.Editor
    {
        private static void Heading(VisualElement root, string text)
        {
            Label heading = new Label(text);
            heading.AddToClassList("audiomw-profile-heading");
            root.Add(heading);
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement { name = "audiomw-parameter-inspector" };
            AudioProfileInspectorLayout.Configure(root);
            Heading(root, "Value range");
            root.Add(new PropertyField(serializedObject.FindProperty("minValue"), "Minimum") { name = "parameter-minimum" });
            root.Add(new PropertyField(serializedObject.FindProperty("maxValue"), "Maximum") { name = "parameter-maximum" });
            root.Add(new Label("Units belong to gameplay. Response bindings normalize this range to 0–1."));
            Heading(root, "Default value");
            root.Add(new PropertyField(serializedObject.FindProperty("defaultValue"), "Stored default") { name = "parameter-default" });
            Label effective = new Label { name = "parameter-effective-default" }; root.Add(effective);
            root.Add(new Label("Used when a parameter store has no value. Reading the default clamps it to the range; opening this Inspector never rewrites the stored value."));
            Heading(root, "Authored scope");
            root.Add(new PropertyField(serializedObject.FindProperty("isGlobal"), "Global parameter") { name = "parameter-global" });
            root.Add(new Label("Global values and emitter-local overrides have separate owners. This flag does not move values between stores or prevent a local override."));
            HelpBox issues = new HelpBox("", HelpBoxMessageType.Warning) { name = "parameter-validation" }; root.Add(issues);
            System.Action refresh = () =>
            {
                List<string> messages = new List<string>();
                foreach (var asset in targets)
                {
                    var item = (SoundParameter)asset;
                    if (float.IsNaN(item.MinValue) || float.IsInfinity(item.MinValue) || float.IsNaN(item.MaxValue) || float.IsInfinity(item.MaxValue) || item.MaxValue < item.MinValue)
                        messages.Add(item.name + ": use finite bounds with Maximum greater than or equal to Minimum.");
                }
                var stored = serializedObject.FindProperty("defaultValue");
                if (targets.Length == 1)
                {
                    var item = (SoundParameter)target;
                    effective.text = "Effective default · " + item.DefaultValue.ToString("0.###");
                    if (float.IsNaN(stored.floatValue) || float.IsInfinity(stored.floatValue)) messages.Add("Stored default must be finite.");
                    else if (stored.floatValue < item.MinValue || stored.floatValue > item.MaxValue) messages.Add("Stored default is outside the range. Runtime reads the clamped value; the stored value is preserved.");
                    if (item.MinValue == item.MaxValue) messages.Add("This range is constant. Normalized input is 0.");
                }
                else effective.text = "Effective defaults · Multiple selected parameters; values remain independent.";
                issues.text = string.Join("\n", messages);
                issues.style.display = messages.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh());
            refresh();
            Button workspace = new Button(() => { var window = AudioInspectionWindow.Open(AudioInspectionWindow.Workspace.Parameters); window.SelectAsset(target); }) { text = "Inspect references and responses", name = "parameter-workspace" };
            workspace.SetEnabled(targets.Length == 1 && EditorUtility.IsPersistent(target));
            root.Add(workspace);
            return root;
        }
    }
}
