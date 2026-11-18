using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    internal sealed class AudioAudibilityPane : Foldout
    {
        private readonly ObjectField target = new ObjectField("Scene source") { objectType = typeof(GameObject), allowSceneObjects = true };
        private readonly Toggle follow = new Toggle("Follow selection") { value = true };
        private readonly PopupField<string> voices = new PopupField<string>("Physical voice", new List<string> { "None" }, 0);
        private readonly Label report = new Label();
        private readonly List<Voice> matches = new List<Voice>();
        private readonly List<string> choices = new List<string>();
        private readonly float[] samples = new float[512];
        private int previousSource;
        private GameObject lastSelection;

        internal AudioAudibilityPane()
        {
            name = "source-audibility"; text = "Live source · Why is it silent?"; value = false;
            Add(follow); Add(target); Add(voices); Add(report);
            report.style.whiteSpace = WhiteSpace.Normal;
            target.RegisterValueChangedCallback(_ => follow.SetValueWithoutNotify(false));
            follow.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) FollowSelection();
            });
            schedule.Execute(Refresh).Every(250);
        }

        private void Refresh()
        {
            if (!value) return;
            if (follow.value && lastSelection != Selection.activeGameObject)
                FollowSelection();
            var go = target.value as GameObject;
            matches.Clear(); choices.Clear();
            if (go != null && go.scene.IsValid() && EditorApplication.isPlaying && AudioRuntime.Exists)
                foreach (var voice in AudioRuntime.Instance.Pool.Voices)
                    if (voice.IsActive && voice.Source != null && (Belongs(voice.AttachTarget, go) || Belongs(voice.Source.transform, go)))
                    {
                        matches.Add(voice);
                        choices.Add((voice.CurrentEvent != null ? voice.CurrentEvent.name : "Event") + " · " + voice.Source.GetInstanceID());
                    }
            voices.style.display = matches.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            var selected = voices.value;
            if (choices.Count == 0) choices.Add("None");
            if (!SameChoices()) voices.choices = new List<string>(choices);
            int index = choices.IndexOf(selected);
            voices.SetValueWithoutNotify(choices[index < 0 ? 0 : index]);
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || go == null || matches.Count == 0)
            {
                previousSource = 0;
                var emitter = go != null ? go.GetComponent<SoundEmitter>() : null;
                report.text = !EditorApplication.isPlaying ? "Enter Play Mode to inspect live audio." : EditorApplication.isPaused ? "Editor paused." :
                    go == null ? "Select the source object in the Hierarchy." :
                    emitter != null && emitter.LatestHandle.IsVirtual ? "Virtual voice · the logical sound is running without a physical source." :
                    "No active physical voice attached to this object or its children. For rejected or position-only requests, inspect the captured events.";
                return;
            }
            var current = matches[Math.Max(index, 0)];
            var source = current.Source;
            var listener = UniqueListener();
            bool outsideLinearRange = false;
            string spatial = "Listener unavailable · exactly one enabled listener is required.";
            if (listener != null)
            {
                float distance = Vector3.Distance(listener.transform.position, source.transform.position);
                outsideLinearRange = source.spatialBlend >= .999f && source.rolloffMode == AudioRolloffMode.Linear && distance >= source.maxDistance;
                string gain = source.rolloffMode == AudioRolloffMode.Linear ?
                    " · modeled gain " + Mathf.Lerp(1, 1-Mathf.InverseLerp(source.minDistance, source.maxDistance, distance), source.spatialBlend).ToString("0.00") : "";
                spatial = distance.ToString("0.0") + " m · " + source.rolloffMode + " " + source.minDistance.ToString("0.#") + "–" + source.maxDistance.ToString("0.#") + " m · spatial " + source.spatialBlend.ToString("0.00") + gain;
            }
            bool warm = previousSource == source.GetInstanceID();
            previousSource = source.GetInstanceID();
            string output = listener == null ? "Output meter unavailable · no unique listener" : "Waiting for sample history";
            if (listener != null)
            {
                int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
                float sourcePeak = 0, listenerPeak = 0;
                for(int c=0;c<channels;c++)
                {
                    source.GetOutputData(samples,c); sourcePeak=Mathf.Max(sourcePeak,Peak(samples));
                    AudioListener.GetOutputData(samples,c); listenerPeak=Mathf.Max(listenerPeak,Peak(samples));
                }
                if (warm) output = "Sample peak · source " + Level(sourcePeak) + " · full mix " + Level(listenerPeak) + (channels==1?" (mono)":" (L/R)");
            }
            var filter = source.GetComponent<AudioLowPassFilter>();
            var group = source.outputAudioMixerGroup;
            string route = group != null ? group.audioMixer.name + " / " + group.name : "Unity default output";
            string state = EditorUtility.audioMasterMute ? "Editor audio output muted" : source.mute ? "Source muted" : AudioListener.pause && !source.ignoreListenerPause ? "Listener paused" :
                AudioListener.volume == 0 ? "Listener muted" : source.isVirtual ? "Unity voice virtual" : outsideLinearRange ? "Outside linear audible range" : source.volume<=0 ? "Source gain is zero" : source.isPlaying ? "Playing" : "Source stopped";
            report.text = (current.CurrentEvent != null ? current.CurrentEvent.name + " · " : "") + state + " · " + (source.clip != null ? source.clip.name : "Missing clip") + "\n" + route + "\n" + spatial +
                "\nBase " + current.BaseVolume.ToString("0.00") + " × parameters " + current.LastVolumeMultiplier.ToString("0.00") + " × blend " + current.LastBlendWeight.ToString("0.00") + " · source " + source.volume.ToString("0.00") + " · occlusion ×" + current.OcclusionVolume.ToString("0.00") +
                (filter != null && filter.enabled ? " · low-pass " + Mathf.RoundToInt(filter.cutoffFrequency) + " Hz" : " · low-pass off") + "\n" + output;
        }

        private void FollowSelection()
        {
            lastSelection = Selection.activeGameObject;
            target.SetValueWithoutNotify(lastSelection != null && lastSelection.scene.IsValid() ? lastSelection : null);
        }

        private static bool Belongs(Transform t, GameObject go) => t != null && (t == go.transform || t.IsChildOf(go.transform));
        private bool SameChoices() => System.Linq.Enumerable.SequenceEqual(choices, voices.choices);
        private static AudioListener UniqueListener()
        {
            AudioListener result = null;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<AudioListener>())
                if (l.isActiveAndEnabled) { if (result != null) return null; result = l; }
            return result;
        }
        private static string Level(float peak) => peak>0 ? (20*Mathf.Log10(peak)).ToString("0.0")+" dBFS" : "silent buffer";
        private static float Peak(float[] block)
        {
            float peak = 0;
            foreach (var sample in block) peak = Mathf.Max(peak, Mathf.Abs(sample));
            return peak;
        }
    }
}
