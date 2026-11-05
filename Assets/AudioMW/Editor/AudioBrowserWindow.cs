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
            public string Folder;
            public bool IsHeader;
            public string HeaderLabel;
            public int Indent;
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
            rootVisualElement.Clear();
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string stylePath = System.IO.Path.GetDirectoryName(scriptPath).Replace('\\', '/') + "/UI/AudioMWStyles.uss";
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);

            if (sheet != null)
            {
                rootVisualElement.styleSheets.Add(sheet);
            }

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
            chipRow.AddToClassList("audiomw-chip-row");
            rootVisualElement.Add(chipRow);

            BuildChips();

            TwoPaneSplitView split = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
            split.viewDataKey = "AudioMW.Browser.Split";
            split.style.flexGrow = 1f;
            rootVisualElement.Add(split);

            listView = new ListView();
            listView.fixedItemHeight = 22f;
            listView.selectionType = SelectionType.Single;
            listView.style.minWidth = 200f;
            listView.AddToClassList("audiomw-list");
            listView.makeItem = MakeRow;
            listView.bindItem = BindRow;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            split.Add(listView);

            preview = new ScrollView(ScrollViewMode.Vertical);
            preview.style.minWidth = 280;
            preview.AddToClassList("audiomw-preview");
            split.Add(preview);

            statusLabel = new Label();
            statusLabel.AddToClassList("audiomw-status");
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
            chip.text = active ? "✓ " + label : label;
            chip.tooltip = active ? "Selected: " + label : "Show " + label.ToLowerInvariant();
            chip.AddToClassList("audiomw-chip");

            if (active)
            {
                chip.AddToClassList("audiomw-chip--active");
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
            Label label = element.Q<Label>("name");
            Image icon = element.Q<Image>("icon");

            element.style.paddingLeft = 6f + entry.Indent * 12f;

            if (entry.IsHeader)
            {
                label.text = entry.HeaderLabel;
                label.AddToClassList("audiomw-group");
                icon.image = entry.Indent == 0 ? null : EditorGUIUtility.IconContent("Folder Icon").image;
                icon.style.width = entry.Indent == 0 ? 0f : 15f;
                return;
            }

            label.RemoveFromClassList("audiomw-group");
            label.text = entry.Asset != null ? entry.Asset.name : "(missing)";
            icon.style.width = 15f;
            icon.image = IconFor(entry.Kind);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is Entry)
                {
                    Entry entry = (Entry)item;

                    if (entry.IsHeader || entry.Asset == null)
                    {
                        break;
                    }

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
            title.AddToClassList("audiomw-title");
            titles.Add(title);

            Label subtitle = new Label(DescribeAsset(entry));
            subtitle.AddToClassList("audiomw-subtitle");
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
                play.text = "Preview clip";
                play.tooltip = "Audition the source clip. Event routing, layers and parameters are evaluated in Play Mode.";
                play.style.width = 96f;
                transport.Add(play);

                Button stop = new Button(EditorAudioPreview.Stop);
                stop.text = "Stop";
                stop.style.width = 56f;
                transport.Add(stop);

                Label facts = new Label(string.Format("{0:F2} s   {1} Hz   {2}",
                    clip.length,
                    clip.frequency,
                    clip.channels == 1 ? "mono" : clip.channels + " ch"));
                facts.AddToClassList("audiomw-facts");
                transport.Add(facts);

                preview.Add(transport);
                preview.Add(BuildMetrics(clip));
            }

            VisualElement note = new VisualElement();
            note.AddToClassList("audiomw-note");

            Label noteLabel = new Label("Editing happens in the standard Inspector");
            noteLabel.AddToClassList("audiomw-note__label");
            note.Add(noteLabel);

            noteLabel.style.whiteSpace = WhiteSpace.Normal;
            preview.Add(note);
            SoundEvent selectedEvent = entry.Asset as SoundEvent;
            if (selectedEvent != null)
            {
                string message = SoundEventValidation.Describe(selectedEvent);
                if (!string.IsNullOrEmpty(message)) preview.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
        }

        private static VisualElement BuildMetrics(AudioClip clip)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 10f;

            LoudnessResult result = Measure(clip);

            row.Add(MakeTile("LUFS", result.HasSignal ? result.IntegratedLufs.ToString("F1") : "n/a"));
            row.Add(MakeTile("dBTP", result.HasSignal ? result.TruePeakDb.ToString("F1") : "n/a"));
            row.Add(MakeTile("PCM size", FormatBytes((long)clip.samples * clip.channels * 4L)));

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
            tile.AddToClassList("audiomw-tile");

            Label caption = new Label(label);
            caption.AddToClassList("audiomw-tile__caption");
            tile.Add(caption);

            Label number = new Label(value);
            number.AddToClassList("audiomw-tile__value");
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
                return FirstEventClip(soundEvent, new HashSet<SoundEvent>());
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

        private static AudioClip FirstEventClip(SoundEvent item, HashSet<SoundEvent> visited)
        {
            if (item == null || visited.Count >= 32 || !visited.Add(item)) return null;
            if (item.ContainerMode == ContainerMode.Simple)
            {
                foreach (AudioClip clip in item.Clips) if (clip != null) return clip;
            }
            else if (item.ContainerMode == ContainerMode.Blend)
            {
                foreach (BlendLayer layer in item.BlendLayers) if (layer != null && layer.Clip != null) return layer.Clip;
            }
            else
            {
                foreach (SoundEventChild child in item.Children)
                {
                    AudioClip clip = FirstEventClip(child != null ? child.Event : null, visited);
                    if (clip != null) return clip;
                }
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

                if (soundEvent.ContainerMode == ContainerMode.Layered || soundEvent.ContainerMode == ContainerMode.Switch)
                    return soundEvent.ContainerMode + " container   " + soundEvent.Children.Length + " child events";
                return ObjectNames.NicifyVariableName(soundEvent.SelectionMode.ToString()) + "   " + soundEvent.Clips.Length + " clips";
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
                    return EditorGUIUtility.IconContent("ScriptableObject Icon").image;

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
                    string path = AssetDatabase.GetAssetPath(asset);
                    string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));

                    all.Add(new Entry
                    {
                        Asset = asset,
                        Kind = kind,
                        Folder = string.IsNullOrEmpty(folder) ? "Assets" : folder
                    });
                }
            }
        }

        private void Rebuild()
        {
            visible.Clear();

            string query = searchField != null ? searchField.value : null;
            bool hasQuery = !string.IsNullOrEmpty(query);

            List<Entry> matching = new List<Entry>();

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

                matching.Add(entry);
            }

            matching.Sort(CompareEntries);

            AssetKind? currentKind = null;
            string currentFolder = null;

            for (int i = 0; i < matching.Count; i++)
            {
                Entry entry = matching[i];

                if (!currentKind.HasValue || currentKind.Value != entry.Kind)
                {
                    currentKind = entry.Kind;
                    currentFolder = null;

                    visible.Add(new Entry
                    {
                        IsHeader = true,
                        HeaderLabel = Plural(entry.Kind),
                        Kind = entry.Kind,
                        Indent = 0
                    });
                }

                if (currentFolder != entry.Folder)
                {
                    currentFolder = entry.Folder;

                    visible.Add(new Entry
                    {
                        IsHeader = true,
                        HeaderLabel = entry.Folder,
                        Kind = entry.Kind,
                        Indent = 1
                    });
                }

                entry.Indent = 2;
                visible.Add(entry);
            }

            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
            }

            if (preview != null)
            {
                int selectedIndex = visible.FindIndex(entry => !entry.IsHeader && entry.Asset == selected);
                if (selectedIndex >= 0)
                {
                    listView.SetSelectionWithoutNotify(new[] { selectedIndex });
                    BuildPreview(visible[selectedIndex]);
                }
                else
                {
                    selected = null;
                    preview.Clear();
                    preview.Add(new HelpBox(matching.Count == 0
                        ? "No matching audio assets. Clear search or filters, or create an event from selected clips."
                        : "Select an asset to inspect its content and preview a source clip. Edit settings in the Inspector.", HelpBoxMessageType.Info));
                }
            }
            UpdateStatus();
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            int byKind = ((int)left.Kind).CompareTo((int)right.Kind);

            if (byKind != 0)
            {
                return byKind;
            }

            int byFolder = string.CompareOrdinal(left.Folder, right.Folder);

            if (byFolder != 0)
            {
                return byFolder;
            }

            return string.CompareOrdinal(left.Asset.name, right.Asset.name);
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

            int shown = 0;

            for (int i = 0; i < visible.Count; i++)
            {
                if (!visible[i].IsHeader)
                {
                    shown++;
                }
            }

            statusLabel.text = string.Format("{0} assets shown of {1}   {2} banks   preview {3}",
                shown,
                all.Count,
                banks,
                EditorAudioPreview.IsAvailable ? "ready" : "unavailable in this Unity version");
        }
    }
}
