using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioEventContextPane : VisualElement
    {
        public AudioEventContextPane(SoundEvent item)
        {
            name = "event-context"; AddToClassList("audiomw-context");
            Add(new Label("Routing & parameters") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            bool parent = item.ContainerMode == ContainerMode.Layered || item.ContainerMode == ContainerMode.Switch;
            if (parent) Add(new Label("Routing belongs to child events. Parent gain, pitch and bindings apply to their leaves."));
            else if (item.MixerGroup != null)
            {
                var group = item.MixerGroup;
                Add(new Button(() => { Selection.activeObject = group; EditorGUIUtility.PingObject(group); }) { text = "Output: " + group.audioMixer.name + " / " + group.name, tooltip = AssetDatabase.GetAssetPath(group), name = "event-route" });
                Add(new Button(() => AssetDatabase.OpenAsset(group.audioMixer)) { text = "Open native AudioMixer" });
            }
            else Add(new Label("Output: Unity default output · No AudioMixerGroup assigned") { name = "event-route" });
            var parameters = new HashSet<SoundParameter>();
            if (item.ContainerMode == ContainerMode.Switch) AddParameter(item.SwitchParameter, "Switch · evaluated at trigger", parameters);
            if (item.ContainerMode == ContainerMode.Blend) AddParameter(item.BlendParameter, "Blend", parameters);
            foreach (var binding in item.ParameterBindings) if (binding != null) AddParameter(binding.Parameter, binding.Target.ToString(), parameters);
            if (parameters.Count == 0) Add(new Label("No valid parameter references on this event. Child events may have their own."));
            Add(new Button(() => AudioWorkspace.OpenEvent(item, "Parameter bindings", "parameterBindings")) { text = "Open parameter bindings" });
            Add(new Label("Runtime audition") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            var emitter = new ObjectField("Emitter") { name = "audition-emitter", objectType = typeof(SoundEmitter), allowSceneObjects = true };
            Add(emitter);
            var status = new Label { name = "audition-status" }; Add(status);
            var play = new Button(() =>
            {
                var source = emitter.value as SoundEmitter;
                if (!string.IsNullOrEmpty(AuditionIssue(item, source))) return;
                EditorAudioPreview.Stop(); source.Play();
            }) { text = "Play event (runtime)", name = "play-event" }; Add(play);
            Add(new Label("Uses the selected emitter's settings and local parameters in the current scene. Requests affect shared selection history; inspect Diagnostics for the actual outcome."));
            System.Action refresh = () =>
            {
                var source = emitter.value as SoundEmitter;
                string issue = AuditionIssue(item, source);
                play.SetEnabled(string.IsNullOrEmpty(issue)); play.tooltip = issue;
                status.text = string.IsNullOrEmpty(issue) ? "Scene: " + source.gameObject.scene.name + " · Listener: " + ActiveListener().name : "Unavailable: " + issue;
            };
            emitter.RegisterValueChangedCallback(_ => refresh()); refresh(); schedule.Execute(refresh).Every(250);
        }

        private void AddParameter(SoundParameter parameter, string role, HashSet<SoundParameter> seen)
        {
            if (parameter == null) { Add(new Label(role + ": missing parameter reference")); return; }
            seen.Add(parameter);
            Add(new Button(() => Selection.activeObject = parameter) { text = role + ": " + parameter.name, tooltip = "Select the actual parameter asset. Defaults are not live runtime values." });
        }

        public static string SelectionLabel(ClipSelectionMode mode)
        {
            switch (mode)
            {
                case ClipSelectionMode.RandomNoRepeat: return "No immediate repeat";
                case ClipSelectionMode.WeightedRandom: return "Weighted random";
                case ClipSelectionMode.ShuffleBag: return "Shuffle bag";
                default: return mode.ToString();
            }
        }

        public static string AuditionIssue(SoundEvent item, SoundEmitter source)
        {
            if (!EditorApplication.isPlaying) return "Enter Play Mode; source preview does not run the event.";
            if (!AudioRuntime.Exists) return "No AudioMW runtime in this scene.";
            if (item == null) return "The event no longer exists.";
            if (source == null || EditorUtility.IsPersistent(source) || !source.gameObject.scene.IsValid()) return "Choose a scene Sound Emitter.";
            if (!source.isActiveAndEnabled) return "The selected emitter is inactive or disabled.";
            if (source.Event != item) return "This emitter uses a different event. Assign it explicitly in the Inspector.";
            if (ActiveListener() == null) return "Exactly one active scene AudioListener is required.";
            if (AudioListener.pause || EditorApplication.isPaused) return "Scene playback is paused.";
            return "";
        }

        private static AudioListener ActiveListener()
        {
            AudioListener found = null;
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
            {
                if (!listener.isActiveAndEnabled) continue;
                if (found != null) return null;
                found = listener;
            }
            return found;
        }
    }
}
