using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class SoundEventSetupWindow : EditorWindow
    {
        private enum Preset { OneShot, Footsteps, UI, Ambience }
        private Preset preset;
        private Label selection;
        private Button create;

        [MenuItem("Window/AudioMW/Create Sound Event")]
        public static void Open()
        {
            SoundEventSetupWindow window = GetWindow<SoundEventSetupWindow>();
            window.titleContent = new GUIContent("Create Sound Event");
            window.minSize = new Vector2(340, 220);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            root.Add(new Label("Create a playable event") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });
            root.Add(new HelpBox("Select audio clips or a folder in the Project window. The preset provides a starting point you can edit in the Inspector.", HelpBoxMessageType.Info));
            EnumField choice = new EnumField("Preset", preset);
            choice.RegisterValueChangedCallback(evt => preset = (Preset)evt.newValue);
            root.Add(choice);
            selection = new Label();
            selection.style.marginTop = selection.style.marginBottom = 12;
            root.Add(selection);
            create = new Button(CreateEvent) { text = "Create event" };
            root.Add(create);
            OnSelectionChange();
        }

        private void OnSelectionChange()
        {
            if (selection == null) return;
            int count = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets).Length;
            selection.text = count == 0 ? "Select at least one audio clip to continue." : count + " audio clips selected";
            create.SetEnabled(count > 0);
        }

        private void CreateEvent()
        {
            AudioClip[] clips = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets);
            if (clips.Length == 0) return;
            string path = EditorUtility.SaveFilePanelInProject("Create sound event", "SFX_" + preset, "asset", "Choose a folder for the event.");
            if (string.IsNullOrEmpty(path)) return;
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            SoundEvent soundEvent = SoundEvent.CreateRuntime(clips);
            soundEvent.SelectionMode = ClipSelectionMode.ShuffleBag;
            soundEvent.SpatialBlend = preset == Preset.UI ? 0f : 1f;
            if (preset == Preset.Footsteps) { soundEvent.MaxInstancesPerEmitter = 2; soundEvent.LimitPolicy = VoiceLimitPolicy.StealOldest; }
            if (preset == Preset.Ambience) { soundEvent.Loop = true; soundEvent.AllowVirtualization = true; }
            AssetDatabase.CreateAsset(soundEvent, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(soundEvent, "Create sound event");
            AssetDatabase.SaveAssets();
            Selection.activeObject = soundEvent;
            EditorGUIUtility.PingObject(soundEvent);
        }
    }
}
