using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(SoundEvent)), CanEditMultipleObjects]
    public sealed class SoundEventInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.style.paddingTop = 4;
            root.style.paddingBottom = 8;
            Field(root, "containerMode", "Playback");
            VisualElement simple = new VisualElement();
            Field(simple, "clips", "Audio clips");
            Foldout references = Section(simple, "Addressed clips", false);
            Field(references, "clipReferences", "Clip references");
            references.Add(new HelpBox("Assign addresses here and include this event in a Sound Bank. Load the bank before playback. Weights follow Audio clips first, then Clip references.", HelpBoxMessageType.Info));
            Field(simple, "selectionMode", "Selection");
            VisualElement weighted = new VisualElement();
            Field(weighted, "clipWeights", "Clip weights");
            weighted.Add(new HelpBox("Weights follow the clip order. Missing weights use 1; zero disables a clip.", HelpBoxMessageType.Info));
            simple.Add(weighted);
            root.Add(simple);
            VisualElement blend = new VisualElement();
            Field(blend, "blendParameter", "Blend parameter");
            Field(blend, "blendLayers", "Layers");
            root.Add(blend);
            VisualElement children = new VisualElement();
            VisualElement switchField = new VisualElement();
            Field(switchField, "switchParameter", "State parameter");
            switchField.Add(new HelpBox("The rounded parameter value selects matching states when the event starts. Pass local parameters to PlayTracked for independent emitters.", HelpBoxMessageType.Info));
            children.Add(switchField);
            Field(children, "children", "Child events");
            children.Add(new HelpBox("Parent gain, pitch and parameter bindings affect every child. Each child owns its loop, spatial settings and mixer route.", HelpBoxMessageType.Info));
            root.Add(children);
            HelpBox diagnostics = new HelpBox("", HelpBoxMessageType.Warning);
            root.Add(diagnostics);

            Foldout sound = Section(root, "Sound", true);
            Field(sound, "volume", "Gain");
            Field(sound, "pitch", "Pitch");
            Foldout variation = Section(sound, "Variation", false);
            Field(variation, "volumeRandomMin", "Gain offset min");
            Field(variation, "volumeRandomMax", "Gain offset max");
            Field(variation, "pitchRandomMin", "Pitch offset min");
            Field(variation, "pitchRandomMax", "Pitch offset max");
            VisualElement leaf = new VisualElement();
            Field(leaf, "loop", "Loop");
            Field(leaf, "ignorePause", "Play while paused");
            Field(leaf, "mixerGroup", "Mixer output");
            sound.Add(leaf);

            Foldout spatial = Section(root, "Spatial audio", false);
            Field(spatial, "attenuationPreset", "Attenuation preset");
            VisualElement spatialValues = new VisualElement();
            Field(spatialValues, "spatialBlend", "3D blend");
            Field(spatialValues, "minDistance", "Near distance");
            Field(spatialValues, "maxDistance", "Far distance");
            Field(spatialValues, "rolloffMode", "Falloff");
            spatial.Add(spatialValues);
            Field(spatial, "allowVirtualization", "Virtualize out of range");
            Foldout limits = Section(root, "Playback limits", false);
            Field(limits, "maxInstances", "Event instances");
            Field(limits, "maxInstancesPerEmitter", "Instances per emitter");
            Field(limits, "cooldownSeconds", "Cooldown (seconds)");
            Field(limits, "limitPolicy", "When full");
            Field(limits, "priority", "Priority (0 is highest)");
            Field(limits, "concurrencyGroup", "Shared limit group");
            limits.Add(new HelpBox("Zero means unlimited or no cooldown. Limits apply to the event you play, including its virtual layers. Position-only calls share one emitter bucket.", HelpBoxMessageType.Info));
            Foldout parameters = Section(root, "Parameter bindings", false);
            Field(parameters, "parameterBindings", "Bindings");

            System.Action refresh = () =>
            {
                SerializedProperty mode = serializedObject.FindProperty("containerMode");
                bool mixed = mode.hasMultipleDifferentValues;
                ContainerMode value = (ContainerMode)mode.enumValueIndex;
                Show(simple, mixed || value == ContainerMode.Simple);
                Show(weighted, serializedObject.FindProperty("selectionMode").hasMultipleDifferentValues ||
                    serializedObject.FindProperty("selectionMode").enumValueIndex == (int)ClipSelectionMode.WeightedRandom);
                Show(blend, mixed || value == ContainerMode.Blend);
                bool parent = value == ContainerMode.Layered || value == ContainerMode.Switch;
                Show(children, mixed || parent);
                Show(switchField, mixed || value == ContainerMode.Switch);
                Show(leaf, mixed || !parent);
                Show(spatial, mixed || !parent);
                spatialValues.SetEnabled(serializedObject.FindProperty("attenuationPreset").objectReferenceValue == null);
                string message = targets.Length == 1 ? SoundEventValidation.Describe((SoundEvent)target) : "";
                diagnostics.text = message;
                Show(diagnostics, !string.IsNullOrEmpty(message));
            };
            root.TrackSerializedObjectValue(serializedObject, _ => refresh());
            refresh();
            return root;
        }

        private void Field(VisualElement parent, string name, string label)
        {
            PropertyField field = new PropertyField(serializedObject.FindProperty(name), label);
            parent.Add(field);
        }

        private static Foldout Section(VisualElement parent, string label, bool expanded)
        {
            Foldout foldout = new Foldout { text = label, value = expanded, viewDataKey = "AudioMW." + label };
            foldout.style.marginTop = 8;
            parent.Add(foldout);
            return foldout;
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
