using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class SoundEventInspectorView : VisualElement
    {
        public Object Asset { get; private set; }
        public SoundEventInspectorView(Object asset) { Asset = asset; }
    }

    [CustomEditor(typeof(SoundEvent)), CanEditMultipleObjects]
    public sealed class SoundEventInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new SoundEventInspectorView(target) { name = "audiomw-event-inspector" };
            AudioWorkspace.Style(root);
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
            weighted.Add(new ClipWeightEditor(serializedObject));
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

            VisualElement sound = new VisualElement();
            sound.Add(new Label("Sound") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, marginBottom = 4 } });
            root.Add(sound);
            DecibelGainField gain = new DecibelGainField("Gain") { name = "volume" };
            gain.BindProperty(serializedObject.FindProperty("volume"));
            sound.Add(gain);
            Field(sound, "pitch", "Pitch ratio");
            Foldout variation = Section(root, "Variation", false);
            Field(variation, "volumeVariationMode", "Gain variation");
            VisualElement decibelVariation = new VisualElement();
            Field(decibelVariation, "volumeRandomDbMin", "Min gain (dB)");
            Field(decibelVariation, "volumeRandomDbMax", "Max gain (dB)");
            variation.Add(decibelVariation);
            VisualElement legacyVariation = new VisualElement();
            Field(legacyVariation, "volumeRandomMin", "Min offset");
            Field(legacyVariation, "volumeRandomMax", "Max offset");
            legacyVariation.Add(new HelpBox("This event keeps its original additive offsets. Switching to Decibels uses a separate range and leaves these values intact.", HelpBoxMessageType.Info));
            variation.Add(legacyVariation);
            Field(variation, "pitchRandomMin", "Min pitch");
            Field(variation, "pitchRandomMax", "Max pitch");
            VisualElement leaf = new VisualElement();
            Field(leaf, "loop", "Loop");
            Field(leaf, "ignorePause", "Play while paused");
            Field(leaf, "mixerGroup", "Mixer output");
            sound.Add(leaf);
            Foldout spatial = Section(root, "Spatial audio", false);
            Field(spatial, "attenuationPreset", "Preset", "An attenuation preset overrides the spatial settings below.");
            VisualElement spatialValues = new VisualElement();
            Field(spatialValues, "spatialBlend", "3D blend");
            Field(spatialValues, "minDistance", "Near (m)");
            Field(spatialValues, "maxDistance", "Far (m)");
            Field(spatialValues, "rolloffMode", "Falloff");
            spatial.Add(spatialValues);
            Field(spatial, "allowVirtualization", "Virtualize", "Keep supported out-of-range 3D voices alive without a physical AudioSource.");
            Foldout limits = Section(root, "Playback limits", false);
            Field(limits, "maxInstances", "Event instances");
            Field(limits, "maxInstancesPerEmitter", "Per emitter", "Maximum simultaneous logical instances on one attached emitter. Zero means unlimited.");
            Field(limits, "cooldownSeconds", "Cooldown (s)");
            Field(limits, "limitPolicy", "When full");
            Field(limits, "priority", "Priority", "0 is highest priority; 256 is lowest.");
            Field(limits, "concurrencyGroup", "Shared group", "A concurrency group shares an instance limit across its events.");
            limits.Add(new HelpBox("Zero means unlimited or no cooldown. Limits apply to the event you play, including its virtual layers. Position-only calls share one emitter bucket.", HelpBoxMessageType.Info));
            Foldout parameters = Section(root, "Parameter bindings", false);
            Field(parameters, "parameterBindings", "Bindings");

            Foldout connect = new Foldout { text = "Use this sound in Unity", value = false, viewDataKey = "AudioMW.Connect" };
            connect.style.marginTop = 8;
            connect.Add(new HelpBox("Assign this event to a Sound Emitter on a scene object. Enter Play Mode to hear event gain, random selection, layers and spatial processing.", HelpBoxMessageType.Info));
            ObjectField destination = new ObjectField("Scene object") { objectType = typeof(GameObject), allowSceneObjects = true, value = Selection.activeGameObject, name = "emitter-target" };
            destination.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            connect.Add(destination);
            Button assign = new Button(() => SoundEventAuthoring.AssignEmitter((SoundEvent)target, destination.value as GameObject)) { text = "Assign event", name = "assign-emitter", tooltip = "Assign to the first Sound Emitter on this scene object, or add one. Supports Undo and prefab overrides." };
            System.Action updateAssign = () =>
            {
                GameObject go = destination.value as GameObject;
                assign.SetEnabled(targets.Length == 1 && go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go));
            };
            destination.RegisterValueChangedCallback(_ => updateAssign());
            connect.Add(assign);
            updateAssign();
            if (targets.Length > 1) connect.Add(new HelpBox("Select one Sound Event before assigning an emitter.", HelpBoxMessageType.Info));
            root.Add(connect);

            System.Action refresh = () =>
            {
                SerializedProperty variationMode = serializedObject.FindProperty("volumeVariationMode");
                Show(decibelVariation, variationMode.hasMultipleDifferentValues || variationMode.enumValueIndex == (int)VolumeVariationMode.Decibels);
                Show(legacyVariation, variationMode.hasMultipleDifferentValues || variationMode.enumValueIndex == (int)VolumeVariationMode.LinearOffsets);
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

        private void Field(VisualElement parent, string name, string label, string tooltip = null)
        {
            PropertyField field = new PropertyField(serializedObject.FindProperty(name), label) { name = name };
            if (!string.IsNullOrEmpty(tooltip)) field.tooltip = tooltip;
            parent.Add(field);
        }

        private Foldout Section(VisualElement parent, string label, bool expanded)
        {
            Foldout foldout = new Foldout { text = label, value = expanded, viewDataKey = "AudioMW." + label };
            foldout.name = "section-" + label;
            foldout.style.marginTop = 8;
            Label summary = new Label { name = "summary", tooltip = "Summary of the current serialized settings." };
            summary.AddToClassList("audiomw-foldout-summary");
            Toggle toggle = foldout.Q<Toggle>();
            toggle.Add(summary);
            System.Action update = () =>
            {
                summary.text = SoundEventSummaries.Get(serializedObject, label);
                summary.style.display = foldout.value ? DisplayStyle.None : DisplayStyle.Flex;
            };
            foldout.RegisterValueChangedCallback(evt => { if (evt.target == foldout) update(); });
            foldout.TrackSerializedObjectValue(serializedObject, _ => update());
            update();
            parent.Add(foldout);
            return foldout;
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
