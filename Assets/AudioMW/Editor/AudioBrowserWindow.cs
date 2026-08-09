using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioBrowserWindow : EditorWindow
    {
        private enum AssetKind
        {
            Event = 0,
            Music = 1,
            Parameter = 2,
            Bank = 3,
            Voice = 4
        }

        private struct Entry
        {
            public Object Asset;
            public AssetKind Kind;
        }

        private static readonly AssetKind[] Kinds =
        {
            AssetKind.Event,
            AssetKind.Music,
            AssetKind.Parameter,
            AssetKind.Bank,
            AssetKind.Voice
        };

        private readonly List<Entry> all = new List<Entry>();
        private readonly List<Entry> visible = new List<Entry>();
        private readonly HashSet<AssetKind> activeFilters = new HashSet<AssetKind>();

        private ListView listView;
        private ToolbarSearchField searchField;
        private VisualElement chipRow;
        private VisualElement preview;
        private Label statusLabel;
        private Object selected;

        [MenuItem("Window/AudioMW/Audio Browser")]
        public static void Open()
        {
            AudioBrowserWindow window = GetWindow<AudioBrowserWindow>();
            window.titleContent = new GUIContent("Audio Browser");
            window.minSize = new Vector2(620f, 360f);
            window.Show();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void OnDisable()
        {
            EditorAudioPreview.Stop();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));

            Toolbar toolbar = new Toolbar();

            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1f;
            searchField.RegisterValueChangedCallback(evt => Rebuild());
            toolbar.Add(searchField);

            ToolbarButton refresh = new ToolbarButton(Refresh);
            refresh.text = "Refresh";
            toolbar.Add(refresh);

            rootVisualElement.Add(toolbar);

            chipRow = new VisualElement();
            chipRow.style.flexDirection = FlexDirection.Row;
            chipRow.style.paddingLeft = 6f;
            chipRow.style.paddingTop = 4f;
            chipRow.style.paddingBottom = 4f;
            chipRow.style.borderBottomWidth = 1f;
            chipRow.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f));
            rootVisualElement.Add(chipRow);

            BuildChips();

            VisualElement split = new VisualElement();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1f;
            rootVisualElement.Add(split);

            listView = new ListView();
            listView.fixedItemHeight = 22f;
            listView.selectionType = SelectionType.Single;
            listView.style.width = 260f;
            listView.style.minWidth = 200f;
            listView.style.borderRightWidth = 1f;
            listView.style.borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f));
            listView.makeItem = MakeRow;
            listView.bindItem = BindRow;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            split.Add(listView);

            preview = new VisualElement();
            preview.style.flexGrow = 1f;
            preview.style.paddingLeft = 12f;
            preview.style.paddingRight = 12f;
            preview.style.paddingTop = 10f;
            preview.style.paddingBottom = 10f;
            split.Add(preview);

            statusLabel = new Label();
            statusLabel.style.paddingLeft = 6f;
            statusLabel.style.paddingTop = 3f;
            statusLabel.style.paddingBottom = 3f;
            statusLabel.style.borderTopWidth = 1f;
            statusLabel.style.borderTopColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f));
            statusLabel.style.fontSize = 11f;
            rootVisualElement.Add(statusLabel);

            Refresh();
        }

        private void BuildChips()
        {
            chipRow.Clear();
            chipRow.Add(MakeChip("All", activeFilters.Count == 0, () =>
            {
                activeFilters.Clear();
                BuildChips();
                Rebuild();
            }));

            for (int i = 0; i < Kinds.Length; i++)
            {
                AssetKind kind = Kinds[i];

                chipRow.Add(MakeChip(Plural(kind), activeFilters.Contains(kind), () =>
                {
                    if (!activeFilters.Remove(kind))
                    {
                        activeFilters.Add(kind);
                    }

                    BuildChips();
                    Rebuild();
                }));
            }
        }

        private static Button MakeChip(string label, bool active, System.Action onClick)
        {
            Button chip = new Button(onClick);
            chip.text = label;
            chip.style.fontSize = 11f;
            chip.style.paddingLeft = 8f;
            chip.style.paddingRight = 8f;
            chip.style.marginRight = 2f;
            chip.style.borderTopLeftRadius = 9f;
            chip.style.borderTopRightRadius = 9f;
            chip.style.borderBottomLeftRadius = 9f;
            chip.style.borderBottomRightRadius = 9f;

            if (active)
            {
                chip.AddToClassList("unity-button--active");
                chip.style.backgroundColor = new StyleColor(new Color(0.24f, 0.48f, 0.86f, 0.28f));
            }

            return chip;
        }

        private static VisualElement MakeRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6f;

            Image icon = new Image();
            icon.name = "icon";
            icon.style.width = 15f;
            icon.style.height = 15f;
            icon.style.marginRight = 6f;
            row.Add(icon);

            Label label = new Label();
            label.name = "name";
            label.style.flexGrow = 1f;
            label.style.fontSize = 12f;
            row.Add(label);

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= visible.Count)
            {
                return;
            }

            Entry entry = visible[index];
            element.Q<Label>("name").text = entry.Asset != null ? entry.Asset.name : "(missing)";
            element.Q<Image>("icon").image = IconFor(entry.Kind);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is Entry)
                {
                    Entry entry = (Entry)item;
                    selected = entry.Asset;
                    Selection.activeObject = entry.Asset;
                    EditorGUIUtility.PingObject(entry.Asset);
                    BuildPreview(entry);
                }

                break;
            }
        }

        private void BuildPreview(Entry entry)
        {
            preview.Clear();

            if (entry.Asset == null)
            {
                return;
            }

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8f;

            Image icon = new Image();
            icon.image = IconFor(entry.Kind);
            icon.style.width = 24f;
            icon.style.height = 24f;
            icon.style.marginRight = 8f;
            header.Add(icon);

            VisualElement titles = new VisualElement();

            Label title = new Label(entry.Asset.name);
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titles.Add(title);

            Label subtitle = new Label(DescribeAsset(entry));
            subtitle.style.fontSize = 11f;
            subtitle.style.opacity = 0.7f;
            titles.Add(subtitle);

            header.Add(titles);
            preview.Add(header);

            AudioClip clip = FirstClip(entry);

            if (clip != null)
            {
                Image waveform = new Image();
                waveform.image = WaveformCache.Get(clip);
                waveform.scaleMode = ScaleMode.StretchToFill;
                waveform.style.height = 64f;
                waveform.style.marginBottom = 6f;
                preview.Add(waveform);

                VisualElement transport = new VisualElement();
                transport.style.flexDirection = FlexDirection.Row;
                transport.style.alignItems = Align.Center;
                transport.style.marginBottom = 10f;

                Button play = new Button(() => EditorAudioPreview.Play(clip));
                play.text = "Play";
                play.style.width = 56f;
                transport.Add(play);

                Button stop = new Button(EditorAudioPreview.Stop);
                stop.text = "Stop";
                stop.style.width = 56f;
                transport.Add(stop);

                Label facts = new Label(string.Format("{0:F2} s   {1} Hz   {2}",
                    clip.length,
                    clip.frequency,
                    clip.channels == 1 ? "mono" : clip.channels + " ch"));
                facts.style.fontSize = 11f;
                facts.style.opacity = 0.7f;
                facts.style.marginLeft = 8f;
                transport.Add(facts);

                preview.Add(transport);
                preview.Add(BuildMetrics(clip));
            }

            VisualElement note = new VisualElement();
            note.style.flexDirection = FlexDirection.Row;
            note.style.alignItems = Align.Center;
            note.style.paddingLeft = 8f;
            note.style.paddingRight = 8f;
            note.style.paddingTop = 6f;
            note.style.paddingBottom = 6f;
            note.style.backgroundColor = new StyleColor(new Color(0.24f, 0.48f, 0.86f, 0.18f));
            note.style.borderTopLeftRadius = 6f;
            note.style.borderTopRightRadius = 6f;
            note.style.borderBottomLeftRadius = 6f;
            note.style.borderBottomRightRadius = 6f;

            Label noteLabel = new Label("Editing happens in the standard Inspector");
            noteLabel.style.fontSize = 11f;
            note.Add(noteLabel);

            preview.Add(note);
        }

        private static VisualElement BuildMetrics(AudioClip clip)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 10f;

            LoudnessResult result = Measure(clip);

            row.Add(MakeTile("Loudness", result.HasSignal ? result.IntegratedLufs.ToString("F1") : "n/a"));
            row.Add(MakeTile("True peak", result.HasSignal ? result.TruePeakDb.ToString("F1") : "n/a"));
            row.Add(MakeTile("Memory", FormatBytes((long)clip.samples * clip.channels * 4L)));

            return row;
        }

        private static LoudnessResult Measure(AudioClip clip)
        {
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.samples <= 0)
            {
                return LoudnessResult.Silent(clip.length);
            }

            float[] data = new float[clip.samples * clip.channels];

            if (!clip.GetData(data, 0))
            {
                return LoudnessResult.Silent(clip.length);
            }

            return LoudnessMeter.Analyze(data, clip.channels, clip.frequency);
        }

        private static VisualElement MakeTile(string label, string value)
        {
            VisualElement tile = new VisualElement();
            tile.style.flexGrow = 1f;
            tile.style.marginRight = 6f;
            tile.style.paddingLeft = 8f;
            tile.style.paddingRight = 8f;
            tile.style.paddingTop = 6f;
            tile.style.paddingBottom = 6f;
            tile.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.12f));
            tile.style.borderTopLeftRadius = 6f;
            tile.style.borderTopRightRadius = 6f;
            tile.style.borderBottomLeftRadius = 6f;
            tile.style.borderBottomRightRadius = 6f;

            Label caption = new Label(label);
            caption.style.fontSize = 11f;
            caption.style.opacity = 0.7f;
            tile.Add(caption);

            Label number = new Label(value);
            number.style.fontSize = 15f;
            tile.Add(number);

            return tile;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024f).ToString("F0") + " KB";
            }

            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
        }

        private static AudioClip FirstClip(Entry entry)
        {
            SoundEvent soundEvent = entry.Asset as SoundEvent;

            if (soundEvent != null)
            {
                if (soundEvent.HasClips && soundEvent.Clips[0] != null)
                {
                    return soundEvent.Clips[0];
                }

                BlendLayer[] layers = soundEvent.BlendLayers;

                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null && layers[i].Clip != null)
                    {
                        return layers[i].Clip;
                    }
                }

                return null;
            }

            MusicTrack track = entry.Asset as MusicTrack;

            if (track != null)
            {
                if (track.LoopClip != null)
                {
                    return track.LoopClip;
                }

                return track.IntroClip;
            }

            VoiceLine line = entry.Asset as VoiceLine;

            if (line != null)
            {
                return line.Clip;
            }

            return null;
        }

        private static string DescribeAsset(Entry entry)
        {
            SoundEvent soundEvent = entry.Asset as SoundEvent;

            if (soundEvent != null)
            {
                if (soundEvent.IsBlendContainer)
                {
                    return "Blend container   " + soundEvent.BlendLayers.Length + " layers";
                }

                return soundEvent.SelectionMode + "   " + soundEvent.Clips.Length + " clips";
            }

            MusicTrack track = entry.Asset as MusicTrack;

            if (track != null)
            {
                return track.Tempo.ToString("F0") + " BPM   " + track.BeatsPerBar + "/4   " + track.Layers.Length + " layers";
            }

            SoundParameter parameter = entry.Asset as SoundParameter;

            if (parameter != null)
            {
                return "Range " + parameter.MinValue.ToString("F2") + " to " + parameter.MaxValue.ToString("F2");
            }

            SoundBank bank = entry.Asset as SoundBank;

            if (bank != null)
            {
                return bank.Events.Length + " events";
            }

            VoiceLine line = entry.Asset as VoiceLine;

            if (line != null)
            {
                return string.IsNullOrEmpty(line.Speaker) ? "Voice line" : line.Speaker;
            }

            return entry.Kind.ToString();
        }

        private static Texture IconFor(AssetKind kind)
        {
            switch (kind)
            {
                case AssetKind.Music:
                    return EditorGUIUtility.IconContent("AudioClip Icon").image;

                case AssetKind.Parameter:
                    return EditorGUIUtility.IconContent("AnimationCurve Icon").image;

                case AssetKind.Bank:
                    return EditorGUIUtility.IconContent("Folder Icon").image;

                case AssetKind.Voice:
                    return EditorGUIUtility.IconContent("AudioSource Icon").image;

                default:
                    return EditorGUIUtility.IconContent("AudioMixerController Icon").image;
            }
        }

        private static string Plural(AssetKind kind)
        {
            switch (kind)
            {
                case AssetKind.Music:
                    return "Music";

                case AssetKind.Parameter:
                    return "Parameters";

                case AssetKind.Bank:
                    return "Banks";

                case AssetKind.Voice:
                    return "Voice";

                default:
                    return "Events";
            }
        }

        private void Refresh()
        {
            all.Clear();
            Collect<SoundEvent>(AssetKind.Event);
            Collect<MusicTrack>(AssetKind.Music);
            Collect<SoundParameter>(AssetKind.Parameter);
            Collect<SoundBank>(AssetKind.Bank);
            Collect<VoiceLine>(AssetKind.Voice);
            Rebuild();
        }

        private void Collect<T>(AssetKind kind) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    all.Add(new Entry { Asset = asset, Kind = kind });
                }
            }
        }

        private void Rebuild()
        {
            visible.Clear();

            string query = searchField != null ? searchField.value : null;
            bool hasQuery = !string.IsNullOrEmpty(query);

            for (int i = 0; i < all.Count; i++)
            {
                Entry entry = all[i];

                if (entry.Asset == null)
                {
                    continue;
                }

                if (activeFilters.Count > 0 && !activeFilters.Contains(entry.Kind))
                {
                    continue;
                }

                if (hasQuery && entry.Asset.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                visible.Add(entry);
            }

            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusLabel == null)
            {
                return;
            }

            int banks = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Kind == AssetKind.Bank)
                {
                    banks++;
                }
            }

            statusLabel.text = string.Format("{0} assets shown of {1}   {2} banks   preview {3}",
                visible.Count,
                all.Count,
                banks,
                EditorAudioPreview.IsAvailable ? "ready" : "unavailable in this Unity version");
        }
    }
}
