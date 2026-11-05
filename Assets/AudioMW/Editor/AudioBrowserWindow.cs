using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioBrowserWindow : EditorWindow
    {
        private enum AssetKind { Event, Music, Parameter, Bank, Voice }
        private struct Entry { public Object Asset; public AssetKind Kind; public string Folder; public bool IsHeader; public string HeaderLabel; }
        [SerializeField] private string query = "";
        [SerializeField] private int filterMask;
        [SerializeField] private string selectedGuid = "";
        [SerializeField] private int sourceIndex;
        [SerializeField] private float libraryWidth = 240;
        [SerializeField] private EditorWindow pinnedInspector;
        [SerializeField] private Object pinnedAsset;
        private readonly List<Entry> all = new List<Entry>();
        private readonly List<Entry> visible = new List<Entry>();
        private ListView listView;
        private ToolbarSearchField searchField;
        private VisualElement chipRow;
        private VisualElement preview;
        private Label statusLabel;
        private Label inspectorStatus;
        private Button followButton;
        private AudioSourcePreviewPane sourcePane;
        private Object selected;
        private int selectedDirty;
        public Object SelectedAsset { get { return selected; } }
        public Object PinnedAsset { get { return pinnedInspector != null ? pinnedAsset : null; } }
        public AudioClip SelectedSource { get { return sourcePane != null ? sourcePane.SelectedClip : null; } }
        public string SearchQuery { get { return query; } set { query = value ?? ""; searchField?.SetValueWithoutNotify(query); Rebuild(); } }
        public int FilterMask { get { return filterMask; } set { filterMask = value; BuildChips(); Rebuild(); } }

        [MenuItem("Window/AudioMW/Audio Browser")]
        public static void Open()
        {
            AudioBrowserWindow window = GetWindow<AudioBrowserWindow>(); window.titleContent = new GUIContent("Sounds");
            window.minSize = new Vector2(620, 360); window.Show();
        }
        private void OnFocus() { if (listView != null) Refresh(); }
        private void OnDisable() { EditorAudioPreview.Stop(); }
        private void OnProjectChange() { if (listView != null) Refresh(); }
        private void OnInspectorUpdate()
        {
            if (selected != null && EditorUtility.GetDirtyCount(selected) != selectedDirty) BuildPreview();
            sourcePane?.Tick(); UpdateInspectorStatus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.Add(AudioWorkspace.Navigation("Sounds"));
            Toolbar toolbar = new Toolbar();
            searchField = new ToolbarSearchField { name = "asset-search", tooltip = "Search audio assets by name." }; searchField.style.flexGrow = 1;
            searchField.SetValueWithoutNotify(query);
            searchField.RegisterValueChangedCallback(evt => { query = evt.newValue; Rebuild(); }); toolbar.Add(searchField);
            toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh" }); rootVisualElement.Add(toolbar);
            chipRow = new VisualElement(); chipRow.AddToClassList("audiomw-chip-row"); rootVisualElement.Add(chipRow); BuildChips();
            TwoPaneSplitView split = new TwoPaneSplitView(0, libraryWidth, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "AudioMW.Browser.Split" };
            split.style.flexGrow = 1; rootVisualElement.Add(split);
            listView = new ListView { name = "asset-list", fixedItemHeight = 24, selectionType = SelectionType.Single };
            listView.style.minWidth = 180; listView.AddToClassList("audiomw-list");
            listView.makeItem = () => new Label();
            listView.bindItem = (element, i) =>
            {
                Label label = (Label)element; Entry entry = visible[i];
                label.text = entry.IsHeader ? entry.HeaderLabel : entry.Asset.name;
                label.tooltip = entry.IsHeader ? entry.HeaderLabel : entry.Asset.name + "\n" + AssetDatabase.GetAssetPath(entry.Asset);
                label.EnableInClassList("audiomw-group", entry.IsHeader);
                label.style.paddingLeft = entry.IsHeader ? 6 : 16;
            };
            listView.itemsSource = visible; listView.selectionChanged += OnSelectionChanged;
            listView.RegisterCallback<GeometryChangedEvent>(evt => { if (evt.newRect.width >= 180) libraryWidth = evt.newRect.width; });
            listView.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return && selected != null) { Selection.activeObject = selected; evt.StopPropagation(); } });
            split.Add(listView);
            preview = new ScrollView { name = "asset-overview" }; preview.style.minWidth = 280; preview.AddToClassList("audiomw-preview"); split.Add(preview);
            statusLabel = new Label { name = "browser-status" }; statusLabel.AddToClassList("audiomw-status"); rootVisualElement.Add(statusLabel);
            Refresh();
        }

        private void BuildChips()
        {
            if (chipRow == null) return;
            chipRow.Clear(); AddChip("All", filterMask == 0, () => FilterMask = 0);
            for (int i = 0; i < 5; i++)
            {
                int bit = 1 << i;
                AddChip(Plural((AssetKind)i), (filterMask & bit) != 0, () => FilterMask ^= bit);
            }
        }
        private void AddChip(string label, bool active, System.Action action)
        {
            Button button = new Button(action) { text = (active ? "✓ " : "") + label, tooltip = "Filter: " + label };
            button.AddToClassList("audiomw-chip"); button.EnableInClassList("audiomw-chip--active", active); chipRow.Add(button);
        }
        public bool SelectAsset(Object asset)
        {
            int index = visible.FindIndex(entry => !entry.IsHeader && entry.Asset == asset);
            if (index < 0) return false;
            listView.SetSelection(index); return true;
        }
        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                Entry entry = (Entry)item;
                if (entry.IsHeader || entry.Asset == null) { selected = null; EditorAudioPreview.Stop(); BuildPreview(); return; }
                if (selected != entry.Asset) { EditorAudioPreview.Stop(); sourceIndex = 0; }
                selected = entry.Asset; selectedGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected));
                Selection.activeObject = selected; BuildPreview(); return;
            }
        }
        public void PinSelection()
        {
            if (selected == null) return;
            if (PinnedAsset == selected) { pinnedInspector.Focus(); return; }
            EditorWindow opened = AudioWorkspace.Pin(selected);
            if (opened != null)
            {
                if (pinnedInspector != null && pinnedInspector != opened) pinnedInspector.Close();
                pinnedInspector = opened; pinnedAsset = selected;
            }
            UpdateInspectorStatus();
        }
        public void FollowSelection()
        {
            if (pinnedInspector != null) pinnedInspector.Close(); pinnedInspector = null; pinnedAsset = null;
            if (selected != null) Selection.activeObject = selected;
            UpdateInspectorStatus();
        }
        private void UpdateInspectorStatus()
        {
            if (inspectorStatus == null) return;
            inspectorStatus.text = PinnedAsset != null ? "Pinned Inspector: " + PinnedAsset.name : "Inspector follows Unity selection";
            inspectorStatus.tooltip = inspectorStatus.text; followButton.style.display = PinnedAsset != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void BuildPreview()
        {
            if (preview == null) return;
            preview.Clear(); sourcePane = null; inspectorStatus = null;
            if (selected == null)
            {
                bool empty = visible.Count == 0;
                preview.Add(new HelpBox(empty ? (all.Count == 0 ? "Create your first Sound Event from clips selected in the Project window." : "No matching assets. Change the search or reset filters.") : "Select an audio asset to view its sources. Settings open automatically in the Inspector.", HelpBoxMessageType.Info));
                if (empty && all.Count > 0) preview.Add(new Button(() => { query = ""; filterMask = 0; searchField.SetValueWithoutNotify(""); BuildChips(); Rebuild(); }) { text = "Reset search and filters", name = "reset-filters" });
                if (all.Count == 0) preview.Add(new Button(SoundEventSetupWindow.Open) { text = "Create sound event" });
                if (PinnedAsset != null)
                {
                    inspectorStatus = new Label { name = "inspector-status", style = { whiteSpace = WhiteSpace.Normal } };
                    followButton = new Button(FollowSelection) { text = "Follow selection", name = "follow-selection" };
                    preview.Add(inspectorStatus); preview.Add(followButton); UpdateInspectorStatus();
                }
                return;
            }
            selectedDirty = EditorUtility.GetDirtyCount(selected);
            Label title = new Label(selected.name) { tooltip = selected.name, name = "asset-title" }; title.AddToClassList("audiomw-title"); preview.Add(title);
            Entry entry = visible.Find(value => !value.IsHeader && value.Asset == selected);
            preview.Add(new Label(DescribeAsset(entry)) { style = { whiteSpace = WhiteSpace.Normal } });
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = Wrap.Wrap;
            actions.Add(new Button(() => Selection.activeObject = selected) { text = "Show settings", tooltip = "Show this asset in the standard Inspector." });
            actions.Add(new Button(PinSelection) { text = "Pin Inspector", name = "pin-inspector", tooltip = "Open a separate native focused Inspector for this asset." }); preview.Add(actions);
            inspectorStatus = new Label { name = "inspector-status", style = { whiteSpace = WhiteSpace.Normal } }; preview.Add(inspectorStatus);
            followButton = new Button(FollowSelection) { text = "Follow selection", name = "follow-selection" }; preview.Add(followButton); UpdateInspectorStatus();
            if (selected is SoundEvent || selected is MusicTrack || selected is VoiceLine)
            {
                sourcePane = new AudioSourcePreviewPane(selected, sourceIndex, index => sourceIndex = index); preview.Add(sourcePane);
                preview.Add(new HelpBox("Enter Play Mode to audition event random selection, layers, routing and spatial processing.", HelpBoxMessageType.Info));
            }
            SoundEvent soundEvent = selected as SoundEvent;
            if (soundEvent != null)
            {
                string message = SoundEventValidation.Describe(soundEvent);
                if (!string.IsNullOrEmpty(message)) preview.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
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
                return ObjectNames.NicifyVariableName(soundEvent.SelectionMode.ToString()) + "   " + soundEvent.TotalClipCount + " clips";
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
            all.Clear(); Collect<SoundEvent>(AssetKind.Event); Collect<MusicTrack>(AssetKind.Music);
            Collect<SoundParameter>(AssetKind.Parameter); Collect<SoundBank>(AssetKind.Bank); Collect<VoiceLine>(AssetKind.Voice); Rebuild();
        }
        private void Collect<T>(AssetKind kind) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) all.Add(new Entry { Asset = asset, Kind = kind, Folder = AssetDatabase.GetAssetPath(asset) });
            }
        }
        private void Rebuild()
        {
            if (listView == null) return;
            visible.Clear(); List<Entry> matching = new List<Entry>();
            foreach (Entry entry in all)
                if (entry.Asset != null && (filterMask == 0 || (filterMask & (1 << (int)entry.Kind)) != 0) &&
                    (string.IsNullOrWhiteSpace(query) || entry.Asset.name.IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)) matching.Add(entry);
            matching.Sort((a, b) => a.Kind == b.Kind ? string.CompareOrdinal(a.Asset.name, b.Asset.name) : a.Kind.CompareTo(b.Kind));
            AssetKind? last = null;
            foreach (Entry entry in matching)
            {
                if (last != entry.Kind) { last = entry.Kind; visible.Add(new Entry { IsHeader = true, HeaderLabel = Plural(entry.Kind), Kind = entry.Kind }); }
                visible.Add(entry);
            }
            listView.itemsSource = visible; listView.Rebuild();
            int index = visible.FindIndex(entry => !entry.IsHeader && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Asset)) == selectedGuid);
            Object next = index >= 0 ? visible[index].Asset : null;
            if (next != selected) EditorAudioPreview.Stop(); selected = next;
            if (index >= 0) listView.SetSelectionWithoutNotify(new[] { index }); else listView.ClearSelection();
            BuildPreview(); statusLabel.text = matching.Count + " of " + all.Count + " assets · Read-only source browser";
        }
    }
}
