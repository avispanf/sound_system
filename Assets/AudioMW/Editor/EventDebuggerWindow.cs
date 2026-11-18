using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class EventDebuggerWindow : EditorWindow
    {
        [SerializeField] private string search = "";
        [SerializeField] private bool rejectionsOnly;
        [SerializeField] private SoundEvent eventFilter;
        [SerializeField] private string librarySearch = "";
        private readonly List<SoundEvent> projectEvents = new List<SoundEvent>();
        private ListView projectLibrary;
        private ObjectField eventFilterField;
        public SoundEvent EventFilter { get { return eventFilter; } }
        private bool compact;
        private Label emptyLabel;
        private VisualElement workspace;
        private VisualElement overview;
        private ScrollView details;
        private ListView listView;
        private ToolbarSearchField searchField;
        private ToolbarToggle rejectionsToggle;
        private Label summaryLabel;
        private Label detailLabel;
        private Label gainLabel;
        private Label captureFacts;
        private Label bankLabel;
        private List<SoundBank> selectedBanks = new List<SoundBank>();
        private Button openEvent;
        private EventDebugRecord selectedRecord;
        private bool hasSelectedRecord;

        private List<EventDebugRecord> visible = new List<EventDebugRecord>();

        [MenuItem("Window/AudioMW/Panels/Diagnostics/Event Debugger")]
        public static void Open()
        {
            EventDebuggerWindow window = GetWindow<EventDebuggerWindow>();
            window.titleContent = new GUIContent("Diagnostics");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        internal static void OpenAudibility()
        {
            Open();
            var root = GetWindow<EventDebuggerWindow>().rootVisualElement;
            root.schedule.Execute(() =>
            {
                var pane = root.Q<AudioAudibilityPane>();
                if (pane != null) pane.value = true;
            });
        }

        private void OnEnable() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
        private void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayModeChanged; }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (summaryLabel != null && (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)) RebuildList();
        }

        public void CreateGUI()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            rootVisualElement.Clear();
            AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.Add(AudioWorkspace.Navigation("Diagnostics"));
            rootVisualElement.AddToClassList("audiomw-inspection");
            rootVisualElement.AddToClassList("audiomw-diagnostics");
            workspace = new VisualElement { name = "diagnostics-layout" };
            overview = new VisualElement { name = "diagnostics-overview" };
            rootVisualElement.Add(workspace); BuildProjectLibrary(); workspace.Add(overview);
            Label eyebrow = new Label("DIAGNOSTICS / CAPTURED REQUESTS");
            eyebrow.AddToClassList("audiomw-eyebrow"); overview.Add(eyebrow);
            Label title = new Label("Why is it silent?");
            title.AddToClassList("audiomw-title"); overview.Add(title);
            Toolbar toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.height = StyleKeyword.Auto;

            searchField = new ToolbarSearchField { value = search };
            searchField.style.flexGrow = 1f;
            searchField.style.flexShrink = 1f;
            searchField.style.flexBasis = 180;
            searchField.style.minWidth = 140;
            searchField.RegisterValueChangedCallback(evt => { search = evt.newValue; RebuildList(); });
            toolbar.Add(searchField);

            rejectionsToggle = new ToolbarToggle { value = rejectionsOnly };
            rejectionsToggle.text = "Rejections only";
            rejectionsToggle.RegisterValueChangedCallback(evt => { rejectionsOnly = evt.newValue; RebuildList(); });
            toolbar.Add(rejectionsToggle);

            ToolbarButton clearButton = new ToolbarButton(() =>
            {
                if (AudioRuntime.Exists)
                {
                    AudioRuntime.Instance.Debugger.Clear();
                }

                RebuildList();
            });
            clearButton.text = "Clear";
            toolbar.Add(clearButton);

            overview.Add(toolbar);
            var filterRow = new VisualElement { name = "diagnostic-event-filter" };
            eventFilterField = new ObjectField("Requested event") { objectType = typeof(SoundEvent), allowSceneObjects = false, value = eventFilter };
            eventFilterField.RegisterValueChangedCallback(evt => SetEventFilter(evt.newValue as SoundEvent));
            filterRow.Add(eventFilterField);
            filterRow.Add(new Button(() => SetEventFilter(null)) { text = "All requests", name = "all-diagnostic-requests" });
            overview.Add(filterRow);

            listView = new ListView { name = "diagnostic-records" };
            listView.fixedItemHeight = compact ? 44f : 28f;
            listView.selectionType = SelectionType.Single;
            listView.style.flexGrow = 1f;
            listView.style.minHeight = 84f;
            VisualElement header = MakeRow(true);
            header.AddToClassList("audiomw-diagnostic-header");
            overview.Add(header);
            listView.makeItem = () => MakeRow(false);
            listView.bindItem = BindItem;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            overview.Add(listView);
            emptyLabel = new Label { name = "diagnostics-empty", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingBottom = 8 } };
            overview.Add(emptyLabel);

            details = new ScrollView { name = "diagnostics-details" };
            details.AddToClassList("audiomw-runtime-card");
            workspace.Add(details);
            details.Add(new AudioAudibilityPane());
            var capturedHeading = new Label("Captured detail"); capturedHeading.AddToClassList("audiomw-panel-heading"); details.Add(capturedHeading);
            detailLabel = new Label { name = "captured-outcome" };
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailLabel.style.paddingLeft = 6f;
            detailLabel.style.paddingTop = 4f;
            detailLabel.style.minHeight = 52f;
            details.Add(detailLabel);
            captureFacts = new Label { name = "captured-facts", style = { whiteSpace = WhiteSpace.Normal, marginTop = 8, marginBottom = 8 } }; details.Add(captureFacts);

            bankLabel = new Label { name = "live-bank-state", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingBottom = 6 } };
            details.Add(bankLabel);
            openEvent = new Button(() =>
            {
                bool limits = selectedRecord.Outcome == PlaybackOutcome.RejectedCooldown || selectedRecord.Outcome == PlaybackOutcome.RejectedInstanceLimit || selectedRecord.Outcome == PlaybackOutcome.RejectedNoVoice;
                SoundEvent settingsEvent = SettingsEvent(selectedRecord);
                AudioWorkspace.OpenEvent(settingsEvent, limits ? "Playback limits" : selectedRecord.Outcome == PlaybackOutcome.RejectedClipNotLoaded && settingsEvent != null && settingsEvent.ContainerMode == ContainerMode.Simple ? "Addressed clips" : null,
                    selectedRecord.Outcome == PlaybackOutcome.RejectedCooldown ? "cooldownSeconds" :
                    selectedRecord.Rejection.HasInstanceLimit ? (selectedRecord.Rejection.Scope == "emitter" ? "maxInstancesPerEmitter" : selectedRecord.Rejection.Scope == "event" ? "maxInstances" : "concurrencyGroup") : null);
            }) { text = "Open event settings", name = "open-event-settings" };
            openEvent.SetEnabled(false); details.Add(openEvent);
            Foldout gains = new Foldout { text = "Gain details · Captured", value = false };
            gainLabel = new Label { name = "captured-gain", style = { whiteSpace = WhiteSpace.Normal } }; gains.Add(gainLabel); details.Add(gains);
            var caveat = new Label("Historical request, not a live output meter. Editing asset settings does not rewrite this capture."); caveat.style.whiteSpace = WhiteSpace.Normal; caveat.AddToClassList("audiomw-capture-note"); details.Add(caveat);
            summaryLabel = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            summaryLabel.style.paddingLeft = 12f;
            summaryLabel.style.paddingRight = 12f;
            summaryLabel.style.flexShrink = 0;
            summaryLabel.style.paddingBottom = 4f;
            rootVisualElement.Add(summaryLabel);

            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetCompact(position.width < 520);
            RebuildList();
            if (hasSelectedRecord) OnSelectionChanged(new object[] { selectedRecord });
        }

        private void BuildProjectLibrary()
        {
            var panel = new VisualElement { name = "diagnostic-project-library" };
            var heading = new Label("Project browser"); heading.AddToClassList("audiomw-panel-heading"); panel.Add(heading);
            var find = new ToolbarSearchField { value = librarySearch, name = "diagnostic-library-search" };
            find.RegisterValueChangedCallback(evt => { librarySearch = evt.newValue; RefreshProjectLibrary(); }); panel.Add(find);
            projectLibrary = new ListView(projectEvents, 26, () => new Label(), (row, i) =>
            {
                var label = (Label)row; var asset = projectEvents[i]; label.text = asset.name; label.tooltip = AssetDatabase.GetAssetPath(asset);
            }) { name = "diagnostic-event-library", selectionType = SelectionType.Single };
            projectLibrary.selectionChanged += selection =>
            {
                foreach (var item in selection) { var asset = item as SoundEvent; SetEventFilter(asset); if (asset != null) Selection.activeObject = asset; break; }
            };
            panel.Add(projectLibrary);
            var note = new Label("Select an event to filter its requests and open native settings."); note.style.whiteSpace = WhiteSpace.Normal; panel.Add(note);
            workspace.Add(panel); RefreshProjectLibrary();
        }

        private void RefreshProjectLibrary()
        {
            if (projectLibrary == null) return;
            projectEvents.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:SoundEvent"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && (string.IsNullOrEmpty(librarySearch) || asset.name.IndexOf(librarySearch, System.StringComparison.OrdinalIgnoreCase) >= 0)) projectEvents.Add(asset);
            }
            projectEvents.Sort((a,b) => string.Compare(a.name,b.name,System.StringComparison.OrdinalIgnoreCase));
            projectLibrary.Rebuild(); RestoreLibrarySelection();
        }

        private void RestoreLibrarySelection()
        {
            if (projectLibrary == null) return;
            int index = projectEvents.IndexOf(eventFilter);
            projectLibrary.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : new int[0]);
        }

        public void SetEventFilter(SoundEvent asset)
        {
            eventFilter = asset; eventFilterField?.SetValueWithoutNotify(asset); RestoreLibrarySelection();
            if (summaryLabel != null) RebuildList();
        }

        public static bool MatchesRequestedEvent(EventDebugRecord record, SoundEvent asset)
        {
            return asset == null || record.Event == asset;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) { SetCompact(evt.newRect.width < 520); }

        private void ReflowDetails()
        {
            if (workspace == null || details == null) return;
            bool stacked = position.width < 900;
            workspace.EnableInClassList("audiomw-diagnostics-stacked", stacked);
            workspace.EnableInClassList("audiomw-diagnostics-hide-library", position.width < 1100);
        }

        private void SetCompact(bool value)
        {
            compact = value;
            ReflowDetails();
            rootVisualElement.EnableInClassList("audiomw-diagnostics-narrow", compact);
            if (listView != null) listView.fixedItemHeight = compact ? 44f : 28f;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                RebuildList();
                UpdateBankState();
            }
        }

        private void OnProjectChange()
        {
            RefreshProjectLibrary();
            if (!hasSelectedRecord || selectedRecord.Outcome != PlaybackOutcome.RejectedClipNotLoaded) return;
            selectedBanks = SettingsEvent(selectedRecord) != null ? AudioSourceCatalog.BanksFor(SettingsEvent(selectedRecord)) : new List<SoundBank>();
            UpdateBankState();
        }

        public static SoundEvent SettingsEvent(EventDebugRecord record)
        {
            return record.Rejection.ProblemEvent != null ? record.Rejection.ProblemEvent : record.Event;
        }

        public static string DescribeBankState(SoundBank bank)
        {
            if (bank == null) return "Bank no longer exists";
            if (bank.IsFullyLoaded()) return bank.name + " · Ready";
            if (bank.PendingStreamCount > 0) return bank.name + " · " + bank.PendingStreamCount + " streamed requests pending";
            return bank.name + (bank.IsLoaded ? " · Audio data not fully ready" : " · Not loaded");
        }

        private void UpdateBankState()
        {
            if (bankLabel == null) return;
            bool show = hasSelectedRecord && selectedRecord.Outcome == PlaybackOutcome.RejectedClipNotLoaded;
            bankLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            bankLabel.text = "Sound Banks · Live now (not captured at rejection)";
            if (SettingsEvent(selectedRecord) == null) bankLabel.text += "\nThe event asset is unavailable.";
            else if (selectedBanks.Count == 0) bankLabel.text += "\nNo containing Sound Bank found. Include this event in a bank and load it before playback.";
            else foreach (SoundBank bank in selectedBanks) bankLabel.text += "\n" + DescribeBankState(bank);
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= visible.Count)
            {
                return;
            }

            EventDebugRecord record = visible[index];
            element.Q<Label>("event").text = string.IsNullOrEmpty(record.EventName) ? "(unnamed)" : record.EventName;
            element.Q<Label>("outcome").text = record.Outcome == PlaybackOutcome.Played ? "Accepted" : "Rejected";
            element.Q<Label>("reason").text = Reason(record);
            element.EnableInClassList("audiomw-request-accepted", record.Outcome == PlaybackOutcome.Played);
            element.EnableInClassList("audiomw-request-rejected", record.Outcome != PlaybackOutcome.Played);
            element.tooltip = record.EventName + "\n" + Reason(record) + "\nCaptured at " + record.Time.ToString("0.00") + " s";
        }

        public static string Reason(EventDebugRecord record)
        {
            return record.Outcome != PlaybackOutcome.Played ? record.DescribeOutcome() :
                !record.HasGainCapture ? "Accepted · gain capture unavailable" : record.FinalVolume == 0 ? "Source volume is zero" : "Playback request accepted";
        }

        private static VisualElement MakeRow(bool header)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("audiomw-diagnostic-row");
            string[] names = { "event", "outcome", "reason" };
            string[] titles = { "Event", "Outcome", "Reason" };
            for (int i = 0; i < names.Length; i++)
            {
                Label cell = new Label(header ? titles[i] : "") { name = names[i] };
                cell.AddToClassList("audiomw-diagnostic-" + names[i]);
                row.Add(cell);
            }
            if (header) row.AddToClassList("audiomw-group");
            return row;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is EventDebugRecord)
                {
                    EventDebugRecord record = (EventDebugRecord)item;

                    selectedRecord = record; hasSelectedRecord = true;
                    selectedBanks = record.Outcome == PlaybackOutcome.RejectedClipNotLoaded && SettingsEvent(record) != null ? AudioSourceCatalog.BanksFor(SettingsEvent(record)) : new List<SoundBank>();
                    UpdateBankState();
                    detailLabel.text = record.EventName + "\n" +
                        (record.Outcome == PlaybackOutcome.Played ? "Accepted" + (record.HasGainCapture && record.FinalVolume == 0 ? " · Source volume is zero" : "") : "Rejected · " + record.DescribeOutcome()) +
                        "\nCaptured at " + record.Time.ToString("0.00") + " s · " + (string.IsNullOrEmpty(record.ClipName) ? "No source clip captured" : record.ClipName);
                    gainLabel.text = record.Outcome == PlaybackOutcome.Played && record.HasGainCapture ? record.DescribeVolumeChain() + "\n" + record.DescribeVolumeDecibels() + "\nCaptured source gain only. Mixer, distance attenuation and master output are not measured here." : "No source-gain values captured for this record.";
                    captureFacts.text = DescribeCapturedFacts(record);
                    UpdateSettingsAction();
                }

                break;
            }
        }

        public static string DescribeCapturedFacts(EventDebugRecord record)
        {
            var capture = record.Rejection;
            if (capture.HasInstanceLimit)
                return "Scope · " + (string.IsNullOrEmpty(capture.Scope) ? "Unavailable" : capture.Scope) + "\nActual / limit · " + capture.CurrentCount + " / " + capture.Limit;
            if (capture.HasCooldown) return "Cooldown remaining · " + capture.RemainingSeconds.ToString("0.###") + " s at capture";
            if (capture.HasVoiceCapacity) return "Requested voices · " + capture.RequestedVoices + "\nAvailable / capacity · " + capture.AvailableVoices + " / " + capture.VoiceCapacity;
            return record.Outcome == PlaybackOutcome.Played ? "Request accepted · Audibility is not verified by this capture." : "Additional limit context unavailable for this rejection.";
        }

        private void UpdateSettingsAction()
        {
            bool available = hasSelectedRecord && SettingsEvent(selectedRecord) != null;
            openEvent.SetEnabled(available);
            openEvent.tooltip = available ? "Select the captured event and reveal relevant settings." : hasSelectedRecord ? "Unavailable: the captured event asset no longer exists." : "Select a captured record with an available event asset.";
        }

        private void RebuildList()
        {
            if (!AudioRuntime.Exists)
            {
                visible = new List<EventDebugRecord>();
                summaryLabel.text = "Runtime not started · Enter Play Mode to capture scene playback.";
            }
            else
            {
                EventDebugger debugger = AudioRuntime.Instance.Debugger;
                visible = debugger.Filter(searchField != null ? searchField.value : null,
                    rejectionsToggle != null && rejectionsToggle.value);
                visible.RemoveAll(record => !MatchesRequestedEvent(record, eventFilter));

                summaryLabel.text = string.Format(
                    "{0} shown of {1} records   accepted {2}   rejected {3}",
                    visible.Count,
                    debugger.Count,
                    debugger.CountWithOutcome(PlaybackOutcome.Played),
                    debugger.Count - debugger.CountWithOutcome(PlaybackOutcome.Played));
            }

            if (emptyLabel != null)
            {
                emptyLabel.text = !AudioRuntime.Exists ? "Enter Play Mode and trigger a Sound Event in the scene." : visible.Count == 0 ? "No matching records. Trigger the requested event, choose All requests, or clear the search and rejection filter." : "";
                emptyLabel.style.display = visible.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                emptyLabel.style.flexGrow = visible.Count == 0 ? 1 : 0;
                if (listView != null) listView.style.display = visible.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
                int selectedIndex = hasSelectedRecord ? visible.IndexOf(selectedRecord) : -1;
                if (selectedIndex >= 0) { listView.SetSelectionWithoutNotify(new[] { selectedIndex }); UpdateSettingsAction(); }
                else
                {
                    hasSelectedRecord = false; selectedBanks.Clear(); UpdateBankState(); detailLabel.text = "Select a captured outcome to inspect its reason.";
                    captureFacts.text = "No captured context selected.";
                    gainLabel.text = "No record selected."; openEvent.SetEnabled(false); openEvent.tooltip = "Select a captured record with an available event asset.";
                }
            }
        }

    }
}
