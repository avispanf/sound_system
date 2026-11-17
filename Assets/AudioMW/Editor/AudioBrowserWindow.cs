using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioBrowserWindow : EditorWindow
    {
        private enum AssetKind { Event, Music, Parameter, Bank, Voice, Asset }
        private struct Entry { public Object Asset; public AssetKind Kind; public string Folder; public bool IsHeader; public string HeaderLabel; }
        [SerializeField] private string query = "";
        [SerializeField] private int filterMask = 19;
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
        private VisualElement workspace;
        private ScrollView contextHost;
        private VisualElement assetContext;
        private Label statusLabel;
        private Label inspectorStatus;
        private Button followButton;
        private AudioSourcePreviewPane sourcePane;
        private Object selected;
        private int selectedDirty;
        private VisualElement structure;
        private VisualElement sourceHost;
        private VisualElement propertiesHost;
        private UnityEditor.Editor embeddedEditor;
        [SerializeField] private bool showRouting;
        private void ReleaseEditor()
        {
            if (propertiesHost != null) { propertiesHost.Unbind(); propertiesHost.Clear(); }
            if (embeddedEditor != null) DestroyImmediate(embeddedEditor);
            embeddedEditor = null;
        }
        public Object SelectedAsset { get { return selected; } }
        public Object PinnedAsset { get { return pinnedInspector != null ? pinnedAsset : null; } }
        public AudioClip SelectedSource { get { return sourcePane != null ? sourcePane.SelectedClip : null; } }
        public string SearchQuery { get { return query; } set { query = value ?? ""; searchField?.SetValueWithoutNotify(query); Rebuild(); } }
        public int FilterMask { get { return filterMask; } set { filterMask = value; BuildChips(); Rebuild(); } }

        [MenuItem("Window/AudioMW/Panels/Authoring/Audio Browser")]
        public static void Open()
        {
            AudioBrowserWindow window = GetWindow<AudioBrowserWindow>(); window.titleContent = new GUIContent("AudioMW Studio");
            window.minSize = new Vector2(620, 360); window.Show();
        }
        private void OnFocus() { if (listView != null) Refresh(); }
        private void OnDisable() { EditorAudioPreview.Stop(); ReleaseEditor(); }
        private void OnProjectChange() { if (listView != null) Refresh(); }
        private void OnInspectorUpdate()
        {
            if (selected != null && EditorUtility.GetDirtyCount(selected) != selectedDirty) RefreshContent();
            sourcePane?.Tick(); UpdateInspectorStatus();
        }

        public void CreateGUI()
        {
            ReleaseEditor(); rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.AddToClassList("audiomw-events");
            rootVisualElement.AddToClassList("audiomw-studio");
            var studioBar = new Toolbar { name = "studio-toolbar" };
            studioBar.Add(new Label("AudioMW / Studio"));
            studioBar.Add(new ToolbarButton(SoundEventSetupWindow.Open) { text = "Create event" });
            studioBar.Add(new ToolbarButton(AudioWorkspace.Mixer) { text = "Mixer" });
            studioBar.Add(new ToolbarButton(AudioWorkspace.Diagnostics) { text = "Diagnostics" });
            studioBar.Add(new ToolbarButton(AudioAgentReviewWindow.Open) { text = "Review changes" });
            rootVisualElement.Add(studioBar);
            var library = new VisualElement { name = "event-library" };
            var heading = new Label("Project"); heading.AddToClassList("audiomw-panel-heading"); library.Add(heading);
            Toolbar toolbar = new Toolbar();
            searchField = new ToolbarSearchField { name = "asset-search", tooltip = "Search audio assets by name." }; searchField.style.flexGrow = 1; searchField.style.flexShrink = 1; searchField.style.minWidth = 0; searchField.style.width = 0;
            searchField.SetValueWithoutNotify(query);
            searchField.RegisterValueChangedCallback(evt => { query = evt.newValue; Rebuild(); }); toolbar.Add(searchField);
            toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh" }); library.Add(toolbar);
            chipRow = new VisualElement { name = "studio-browser-tabs" }; chipRow.AddToClassList("audiomw-chip-row"); library.Add(chipRow); BuildChips();
            TwoPaneSplitView split = new TwoPaneSplitView(0, libraryWidth, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "AudioMW.Browser.Split" };
            workspace = new VisualElement { name = "events-layout" }; rootVisualElement.Add(workspace);
            split.style.flexGrow = 1; split.style.minWidth = 0; workspace.Add(split);
            listView = new ListView { name = "asset-list", fixedItemHeight = 24, selectionType = SelectionType.Single };
            listView.style.flexGrow = 1;
            library.style.minWidth = 180;
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
            library.RegisterCallback<GeometryChangedEvent>(evt => { if (evt.newRect.width >= 180) libraryWidth = evt.newRect.width; });
            listView.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return && selected != null) { Selection.activeObject = selected; evt.StopPropagation(); } });
            library.Add(listView); split.Add(library);
            preview = new ScrollView { name = "asset-overview" }; preview.style.minWidth = 280; preview.AddToClassList("audiomw-preview"); split.Add(preview);
            contextHost = new ScrollView { name = "events-context-host" }; workspace.Add(contextHost);
            workspace.RegisterCallback<GeometryChangedEvent>(_ => PlaceContext());
            statusLabel = new Label { name = "browser-status" }; statusLabel.AddToClassList("audiomw-status"); rootVisualElement.Add(statusLabel);
            Refresh();
        }

        private void PlaceContext()
        {
            if (workspace == null || assetContext == null) return;
            bool narrow = workspace.layout.width < 1100;
            workspace.EnableInClassList("audiomw-events-narrow", narrow);
            if (narrow) { if (assetContext.parent != ((ScrollView)preview).contentContainer) preview.Add(assetContext); }
            else if (assetContext.parent != contextHost.contentContainer) contextHost.Add(assetContext);
        }

        private void BuildChips()
        {
            if (chipRow == null) return;
            chipRow.Clear();
            if (filterMask == 0) AddChip("All", true, () => FilterMask = 0);
            AddChip("Events", filterMask == 19 || filterMask == 1 || filterMask == 2 || filterMask == 16, () => FilterMask = 19);
            AddChip("Banks", filterMask == 8, () => FilterMask = 8);
            AddChip("Assets", filterMask == 32, () => FilterMask = 32);
            AddChip("Parameters", filterMask == 4, () => FilterMask = 4);
        }

        private void AddChip(string label, bool active, System.Action action)
        {
            Button button = new Button(action) { text = label, tooltip = "Filter: " + label };
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
            ReleaseEditor(); preview.Clear(); contextHost.Clear(); sourcePane = null; inspectorStatus = null;
            assetContext = new VisualElement { name = "asset-context" };
            if (!(selected is SoundEvent)) showRouting = false;
            var contextTabs = new Toolbar();
            var propertiesButton = new ToolbarButton(() => { showRouting = false; BuildPreview(); }) { text = "Properties" };
            var routingButton = new ToolbarButton(() => { showRouting = true; BuildPreview(); }) { text = "Routing / audition" };
            propertiesButton.SetEnabled(showRouting); routingButton.SetEnabled(!showRouting && selected is SoundEvent);
            contextTabs.Add(propertiesButton); contextTabs.Add(routingButton); assetContext.Add(contextTabs);
            if (selected == null)
            {
                bool empty = visible.Count == 0;
                preview.Add(new HelpBox(empty ? (all.Count == 0 ? "Create your first Sound Event from clips selected in the Project window." : "No matching assets. Change the search or reset filters.") : "Select an event to edit its container, properties and sources here.", HelpBoxMessageType.Info));
                if (empty && all.Count > 0) preview.Add(new Button(() => { query = ""; filterMask = 19; searchField.SetValueWithoutNotify(""); BuildChips(); Rebuild(); }) { text = "Reset search and filters", name = "reset-filters" });
                if (all.Count == 0) preview.Add(new Button(SoundEventSetupWindow.Open) { text = "Create sound event" });
                if (PinnedAsset != null)
                {
                    inspectorStatus = new Label { name = "inspector-status", style = { whiteSpace = WhiteSpace.Normal } };
                    followButton = new Button(FollowSelection) { text = "Follow selection", name = "follow-selection" };
                    assetContext.Add(inspectorStatus); assetContext.Add(followButton); UpdateInspectorStatus();
                }
                PlaceContext(); return;
            }
            selectedDirty = EditorUtility.GetDirtyCount(selected);
            Label context = new Label((selected is SoundBank ? "BANK EDITOR · " : selected is SoundParameter ? "PARAMETER EDITOR · " : selected is AudioClip ? "AUDIO BIN · " : "EVENT EDITOR · ") + EntryKind(selected)) { name = "overview-context" }; context.AddToClassList("audiomw-eyebrow"); preview.Add(context);
            Label title = new Label(selected.name) { tooltip = selected.name, name = "asset-title" }; title.AddToClassList("audiomw-title"); preview.Add(title);
            Entry entry = visible.Find(value => !value.IsHeader && value.Asset == selected);
            preview.Add(new Label(DescribeAsset(entry)) { style = { whiteSpace = WhiteSpace.Normal } });
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = Wrap.Wrap;
            actions.Add(new Button(() => Selection.activeObject = selected) { text = "Show settings", tooltip = "Show this asset in the standard Inspector." });
            actions.Add(new Button(PinSelection) { text = "Pin Inspector", name = "pin-inspector", tooltip = "Open a separate native focused Inspector for this asset." }); assetContext.Add(actions);
            inspectorStatus = new Label { name = "inspector-status", style = { whiteSpace = WhiteSpace.Normal } }; assetContext.Add(inspectorStatus);
            followButton = new Button(FollowSelection) { text = "Follow selection", name = "follow-selection" }; assetContext.Add(followButton); UpdateInspectorStatus();
            propertiesHost = new VisualElement { name = "studio-properties" };
            assetContext.Add(propertiesHost);
            if (showRouting && selected is SoundEvent auditionEvent) propertiesHost.Add(new AudioEventContextPane(auditionEvent));
            else if (selected is AudioClip clip)
            {
                propertiesHost.Add(new Label("Audio file") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                propertiesHost.Add(new Label(AudioSourcePreviewPane.SourceFormat(clip)));
                propertiesHost.Add(new Label(clip.frequency + " Hz · " + clip.loadType));
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                propertiesHost.Add(new Label(importer != null ? "Force to mono: " + (importer.forceToMono ? "On" : "Off") : "Import settings unavailable: this clip has no AudioImporter."));
                propertiesHost.Add(new Label(AssetDatabase.GetAssetPath(clip)) { style = { whiteSpace = WhiteSpace.Normal } });
                propertiesHost.Add(new Button(PinSelection) { text = "Edit import settings in Unity" });
                propertiesHost.Add(new Label("Import changes use Unity's native Apply / Revert workflow.") { style = { whiteSpace = WhiteSpace.Normal } });
            }
            else
            {
                if (showRouting) propertiesHost.Add(new HelpBox("Runtime event audition is available for Sound Events. Edit this asset below.", HelpBoxMessageType.Info));
                embeddedEditor = UnityEditor.Editor.CreateEditor(selected);
                propertiesHost.Add(new InspectorElement(embeddedEditor));
            }
            structure = new VisualElement { name = "studio-container" }; preview.Add(structure);
            sourceHost = new VisualElement { name = "studio-audio-bin" }; preview.Add(sourceHost);
            RefreshContent();
            SoundEvent soundEvent = selected as SoundEvent;
            if (soundEvent != null)
            {
                string message = SoundEventValidation.Describe(soundEvent);
                if (!string.IsNullOrEmpty(message)) assetContext.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
            PlaceContext();
        }
        private void RefreshContent()
        {
            if (selected == null || structure == null || sourceHost == null) return;
            selectedDirty = EditorUtility.GetDirtyCount(selected);
            structure.Clear(); sourceHost.Clear();
            structure.Add(new Label(selected is SoundEvent ? "Container" : "Content") { name = "studio-container-heading" });
            var item = selected as SoundEvent;
            if (item != null)
            {
                var flow = new VisualElement { name = "studio-signal-flow" };
                flow.Add(new Label("TRIGGER"));
                flow.Add(new Label("→"));
                flow.Add(new Label(item.ContainerMode == ContainerMode.Simple ? AudioEventContextPane.SelectionLabel(item.SelectionMode) : item.ContainerMode.ToString()));
                flow.Add(new Label("→"));
                flow.Add(new Label(item.ContainerMode == ContainerMode.Layered || item.ContainerMode == ContainerMode.Switch ? "Child outputs" : item.MixerGroup != null ? item.MixerGroup.name : "Unity output"));
                structure.Add(flow);
            }
            var sources = AudioSourceCatalog.Collect(selected);
            if (sources.Count > 0)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    int index = i;
                    var row = new Button(() => sourcePane?.Select(index)) { text = (i + 1).ToString("00") + "   " + sources[i].Name, tooltip = sources[i].Location };
                    row.AddToClassList("studio-instrument"); structure.Add(row);
                }
                sourceHost.Add(new Label("Audio Bin · source audition") { name = "studio-bin-heading" });
                sourcePane = new AudioSourcePreviewPane(selected, sourceIndex, index => { sourceIndex = index; UpdateSourceSelection(); }); sourceHost.Add(sourcePane);
                sourceIndex = sourcePane.Index; UpdateSourceSelection();
            }
            else
            {
                sourcePane = null;
                if (selected is SoundBank bank)
                {
                    foreach (var member in bank.Events)
                    {
                        var target = member;
                        var row = new Button(() => { FilterMask = 19; SearchQuery = ""; SelectAsset(target); }) { text = target != null ? target.name : "Missing event reference" };
                        row.SetEnabled(target != null); row.AddToClassList("studio-bank-member"); structure.Add(row);
                    }
                    if (bank.Events.Length == 0) structure.Add(new Label("No member events. Assign events in Properties."));
                }
                else if (selected is SoundParameter parameter)
                {
                    structure.Add(new Label("Range: " + parameter.MinValue.ToString("0.###") + " → " + parameter.MaxValue.ToString("0.###")));
                    structure.Add(new Label("Default: " + parameter.DefaultValue.ToString("0.###")));
                    structure.Add(new Label("Asset defaults · not live runtime values"));
                }
                else structure.Add(new Label("No playable source clips. Add content in Properties."));
            }
        }
        private void UpdateSourceSelection()
        {
            int index = 0;
            structure.Query<Button>(className: "studio-instrument").ForEach(button => button.EnableInClassList("studio-instrument-selected", index++ == sourceIndex));
        }
        private static string EntryKind(Object asset) { return asset is SoundEvent ? "SOUND EVENT" : ObjectNames.NicifyVariableName(asset.GetType().Name).ToUpperInvariant(); }

        private static string DescribeAsset(Entry entry)
        {
            SoundEvent soundEvent = entry.Asset as SoundEvent;

            if (soundEvent != null)
            {
                if (soundEvent.ContainerMode == ContainerMode.Blend)
                {
                    return "Blend container   " + soundEvent.BlendLayers.Length + " layers";
                }

                if (soundEvent.ContainerMode == ContainerMode.Layered || soundEvent.ContainerMode == ContainerMode.Switch)
                    return soundEvent.ContainerMode + " container   " + soundEvent.Children.Length + " child events";
                return "Random / Sequence · " + AudioEventContextPane.SelectionLabel(soundEvent.SelectionMode) + " · " + soundEvent.TotalClipCount + " clips";
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
                case AssetKind.Asset:
                    return "Assets";
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
            Collect<SoundParameter>(AssetKind.Parameter); Collect<SoundBank>(AssetKind.Bank); Collect<VoiceLine>(AssetKind.Voice); Collect<AudioClip>(AssetKind.Asset); Rebuild();
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
            BuildPreview(); statusLabel.text = matching.Count + " of " + all.Count + " assets · Native Unity authoring";
        }
    }
}
