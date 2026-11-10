using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioSourcePreviewPane : VisualElement
    {
        private readonly List<AudioSourceEntry> sources;
        private readonly Action<int> changed;
        private readonly VisualElement detail = new VisualElement { name = "source-detail" };
        private readonly ListView list;
        private AudioClip[] listedClips;
        private AudioDataLoadState[] listedStates;
        private AudioClip lastClip;
        private AudioDataLoadState lastState;
        public int Index { get; private set; }
        public AudioClip SelectedClip { get { return Index >= 0 && Index < sources.Count ? sources[Index].Clip : null; } }

        public AudioSourcePreviewPane(UnityEngine.Object asset, int index, Action<int> changed)
        {
            this.changed = changed;
            sources = AudioSourceCatalog.Collect(asset);
            listedClips = new AudioClip[sources.Count];
            listedStates = new AudioDataLoadState[sources.Count];
            name = "source-preview";
            Add(new Label("Source clips · " + sources.Count) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, marginBottom = 4 } });
            if (sources.Count == 0)
            {
                EditorAudioPreview.Stop();
                Add(new HelpBox("No source clips. Add content in the Inspector.", HelpBoxMessageType.Info));
                Index = -1; return;
            }
            list = new ListView(sources, 24, () => new Label(), (element, i) =>
            {
                Label label = (Label)element; label.text = sources[i].Name;
                label.tooltip = sources[i].Name + "\n" + sources[i].Location;
            }) { name = "source-list", selectionType = SelectionType.Single };
            list.style.height = Math.Min(sources.Count, 5) * 24;
            list.style.minHeight = 24;
            list.selectionChanged += selection => { foreach (object item in selection) { Select(sources.IndexOf((AudioSourceEntry)item)); break; } };
            Add(list);
            Add(detail);
            Index = Mathf.Clamp(index, 0, sources.Count - 1);
            list.SetSelectionWithoutNotify(new[] { Index });
            ShowCurrent();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= sources.Count || index == Index) return;
            EditorAudioPreview.Stop(); Index = index;
            list.SetSelectionWithoutNotify(new[] { index });
            changed?.Invoke(index);
            ShowCurrent();
        }

        public void Tick()
        {
            bool refresh = false;
            for (int i = 0; i < sources.Count; i++)
            {
                AudioClip current = sources[i].Clip;
                AudioDataLoadState state = current != null ? current.loadState : AudioDataLoadState.Unloaded;
                if (listedClips[i] != current || listedStates[i] != state) refresh = true;
                listedClips[i] = current; listedStates[i] = state;
            }
            if (refresh) list?.RefreshItems();
            AudioClip clip = SelectedClip;
            if (clip != lastClip || (clip != null && clip.loadState != lastState)) ShowCurrent();
        }

        private void ShowCurrent()
        {
            detail.Clear();
            if (Index < 0 || Index >= sources.Count) return;
            AudioSourceEntry source = sources[Index]; AudioClip clip = source.Clip;
            if (EditorAudioPreview.CurrentClip != null && EditorAudioPreview.CurrentClip != clip) EditorAudioPreview.Stop();
            lastClip = clip; lastState = clip != null ? clip.loadState : AudioDataLoadState.Unloaded;
            detail.Add(new Label(source.Name) { name = "source-name", tooltip = source.Name + "\n" + source.Location, style = { marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal } });
            string issue = source.PreviewIssue;
            if (clip != null)
            {
                detail.Add(new Label(string.Format("{0:0.00} s · {1} Hz · {2} ch", clip.length, clip.frequency, clip.channels)) { name = "source-facts", style = { whiteSpace = WhiteSpace.Normal } });
                bool directSource = source.Reference == null || !source.Reference.IsAddressed;
                if ((clip.loadState == AudioDataLoadState.Loaded || (directSource && clip.loadState == AudioDataLoadState.Unloaded)) && clip.loadType == AudioClipLoadType.DecompressOnLoad)
                {
                    if (TryMeasure(clip, out LoudnessResult result))
                    {
                        detail.Add(new Image { name = "source-waveform", image = WaveformCache.Get(clip), scaleMode = ScaleMode.StretchToFill, tooltip = "Waveform: " + clip.name, style = { height = 64, marginTop = 6, marginBottom = 6 } });
                        string loudness = result.HasSignal ? result.IntegratedLufs.ToString("0.0") + " LUFS · " + result.TruePeakDb.ToString("0.0") + " dBTP" : "No measurable signal";
                        detail.Add(new Label(loudness + " · PCM " + (clip.samples * (double)clip.channels * 4 / 1048576).ToString("0.##") + " MB") { name = "source-metrics", style = { whiteSpace = WhiteSpace.Normal } });
                    }
                    else detail.Add(new HelpBox("Unavailable: Unity could not read PCM samples. Waveform and loudness are not measured.", HelpBoxMessageType.Warning));
                }
                else detail.Add(new Label("Unavailable: waveform and loudness require loaded, decompressed audio.") { style = { whiteSpace = WhiteSpace.Normal } });
            }
            lastState = clip != null ? clip.loadState : AudioDataLoadState.Unloaded;
            VisualElement transport = new VisualElement(); transport.style.flexDirection = FlexDirection.Row; transport.style.flexWrap = Wrap.Wrap;
            Button play = new Button(() => EditorAudioPreview.Play(SelectedClip)) { text = "Play source (dry)", name = "preview-source", tooltip = string.IsNullOrEmpty(issue) ? "Play this original AudioClip without event processing." : issue };
            play.SetEnabled(string.IsNullOrEmpty(issue)); transport.Add(play);
            transport.Add(new Button(EditorAudioPreview.Stop) { text = "Stop", name = "stop-source" });
            detail.Add(transport);
            detail.Add(new Label("Original file · No event processing") { name = "source-disclaimer", style = { whiteSpace = WhiteSpace.Normal, marginTop = 4 } });
            if (!string.IsNullOrEmpty(issue)) detail.Add(new HelpBox(issue, HelpBoxMessageType.Info));
            if (source.Reference != null && source.Reference.IsAddressed && !string.IsNullOrEmpty(issue))
            {
                List<SoundBank> banks = AudioSourceCatalog.BanksFor(source.Owner);
                foreach (SoundBank bank in banks)
                {
                    string status = bank.IsFullyLoaded() ? "Ready" : bank.PendingStreamCount > 0 ? bank.PendingStreamCount + " pending" : bank.IsLoaded ? "Clips unavailable" : "Not loaded";
                    detail.Add(new Button(() => Selection.activeObject = bank) { text = bank.name + " · " + status, tooltip = "Open this Sound Bank in the Inspector. Loading remains owned by the scene/runtime." });
                }
                if (banks.Count == 0) detail.Add(new Label("Include this event in a Sound Bank and load it through your scene or bootstrap.") { style = { whiteSpace = WhiteSpace.Normal } });
            }
        }

        private static bool TryMeasure(AudioClip clip, out LoudnessResult result)
        {
            float[] samples = new float[clip.samples * clip.channels];
            result = default;
            if (!clip.GetData(samples, 0)) return false;
            result = LoudnessMeter.Analyze(samples, clip.channels, clip.frequency); return true;
        }
    }
}
