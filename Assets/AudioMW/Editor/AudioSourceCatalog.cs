using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public sealed class AudioSourceEntry
    {
        public AudioClip Direct;
        public ClipReference Reference;
        public SoundEvent Owner;
        public string Location;
        public AudioClip Clip { get { return Reference != null ? Reference.Clip : Direct; } }
        public string Name
        {
            get { return Clip != null ? Clip.name : Reference != null && Reference.IsAddressed ? Reference.Address + " · Not loaded" : "Missing clip"; }
        }
        public string PreviewIssue
        {
            get
            {
                if (Clip == null) return Reference != null && Reference.IsAddressed
                    ? "Addressed source is not loaded. Load its Sound Bank before preview."
                    : "Assign an AudioClip to this source in the Inspector.";
                if (Clip.loadState == AudioDataLoadState.Failed) return "Audio data failed to load. Check the clip import settings.";
                if (Clip.loadState == AudioDataLoadState.Loading) return "Audio data is loading. Wait for loading to finish.";
                if (Reference != null && Reference.IsAddressed && Clip.loadState != AudioDataLoadState.Loaded)
                    return "Addressed audio data is not loaded. Load its Sound Bank before preview.";
                if (!EditorAudioPreview.IsAvailable) return "Source preview is unavailable in this Unity version. Use Play Mode.";
                return "";
            }
        }
    }

    public static class AudioSourceCatalog
    {
        public static List<AudioSourceEntry> Collect(Object asset)
        {
            List<AudioSourceEntry> sources = new List<AudioSourceEntry>();
            SoundEvent item = asset as SoundEvent;
            if (item != null) Visit(item, sources, new HashSet<SoundEvent>(), item.name);
            MusicTrack track = asset as MusicTrack;
            if (track != null)
            {
                if (track.IntroClip != null) sources.Add(new AudioSourceEntry { Direct = track.IntroClip, Location = "Intro" });
                if (track.LoopClip != null) sources.Add(new AudioSourceEntry { Direct = track.LoopClip, Location = "Body" });
                foreach (MusicLayer layer in track.Layers) if (layer != null) sources.Add(new AudioSourceEntry { Direct = layer.Clip, Location = layer.Name });
            }
            VoiceLine line = asset as VoiceLine;
            if (line != null) sources.Add(new AudioSourceEntry { Direct = line.Clip, Location = line.Speaker });
            return sources;
        }

        private static void Visit(SoundEvent item, List<AudioSourceEntry> sources, HashSet<SoundEvent> path, string location)
        {
            if (item == null || path.Count >= 32 || !path.Add(item)) return;
            if (item.ContainerMode == ContainerMode.Simple)
            {
                for (int i = 0; i < item.Clips.Length; i++) sources.Add(new AudioSourceEntry { Direct = item.Clips[i], Owner = item, Location = location + " / Clip " + (i + 1) });
                for (int i = 0; i < item.ClipReferences.Length; i++) sources.Add(new AudioSourceEntry { Reference = item.ClipReferences[i], Owner = item, Location = location + " / Reference " + (i + 1) });
            }
            else if (item.ContainerMode == ContainerMode.Blend)
            {
                for (int i = 0; i < item.BlendLayers.Length; i++) if (item.BlendLayers[i] != null)
                    sources.Add(new AudioSourceEntry { Direct = item.BlendLayers[i].Clip, Owner = item, Location = location + " / Layer " + (i + 1) });
            }
            else foreach (SoundEventChild child in item.Children)
                if (child != null && child.Event != null) Visit(child.Event, sources, path, location + " / " + (string.IsNullOrEmpty(child.Label) ? child.Event.name : child.Label));
            path.Remove(item);
        }

        public static List<SoundBank> BanksFor(SoundEvent item)
        {
            List<SoundBank> banks = new List<SoundBank>();
            foreach (string guid in AssetDatabase.FindAssets("t:SoundBank"))
            {
                SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(AssetDatabase.GUIDToAssetPath(guid));
                if (bank == null) continue;
                foreach (SoundEvent root in bank.Events)
                    if (Contains(root, item, new HashSet<SoundEvent>())) { banks.Add(bank); break; }
            }
            return banks;
        }

        private static bool Contains(SoundEvent root, SoundEvent item, HashSet<SoundEvent> visited)
        {
            if (root == null || !visited.Add(root)) return false;
            if (root == item) return true;
            foreach (SoundEventChild child in root.Children) if (child != null && Contains(child.Event, item, visited)) return true;
            return false;
        }
    }
}
