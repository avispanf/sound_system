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
        private Label emptyLabel;
        private ListView listView;
        private ToolbarSearchField searchField;
        private ToolbarToggle rejectionsToggle;
        private Label summaryLabel;
        private Label detailLabel;
        private Label gainLabel;
        private Label bankLabel;
        private List<SoundBank> selectedBanks = new List<SoundBank>();
        private Button openEvent;
        private EventDebugRecord selectedRecord;
        private bool hasSelectedRecord;

        private List<EventDebugRecord> visible = new List<EventDebugRecord>();

        [MenuItem("Window/AudioMW/Event Debugger")]
        public static void Open()
        {
            EventDebuggerWindow window = GetWindow<EventDebuggerWindow>();
            window.titleContent = new GUIContent("Diagnostics");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.Add(AudioWorkspace.Navigation("Diagnostics"));
            rootVisualElement.AddToClassList("audiomw-inspection");
            Label eyebrow = new Label("DIAGNOSTICS / CAPTURED REQUESTS");
            eyebrow.AddToClassList("audiomw-eyebrow"); rootVisualElement.Add(eyebrow);
            Label title = new Label("Why is it silent?");
            title.AddToClassList("audiomw-title"); rootVisualElement.Add(title);
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

            rootVisualElement.Add(toolbar);

            listView = new ListView();
            listView.fixedItemHeight = 28f;
            listView.selectionType = SelectionType.Single;
            listView.style.flexGrow = 1f;
            listView.style.minHeight = 84f;
            rootVisualElement.Add(MakeRow(true));
            listView.makeItem = () => MakeRow(false);
            listView.bindItem = BindItem;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(listView);
            emptyLabel = new Label { name = "diagnostics-empty", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingBottom = 8 } };
            rootVisualElement.Add(emptyLabel);

            ScrollView details = new ScrollView { name = "diagnostics-details" };
            details.style.maxHeight = Length.Percent(50);
            details.style.flexShrink = 1;
            details.AddToClassList("audiomw-runtime-card");
            rootVisualElement.Add(details);
            detailLabel = new Label();
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailLabel.style.paddingLeft = 6f;
            detailLabel.style.paddingTop = 4f;
            detailLabel.style.minHeight = 52f;
            details.Add(detailLabel);

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
            gainLabel = new Label { style = { whiteSpace = WhiteSpace.Normal } }; gains.Add(gainLabel); details.Add(gains);
            summaryLabel = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            summaryLabel.style.paddingLeft = 6f;
            summaryLabel.style.paddingBottom = 4f;
            rootVisualElement.Add(summaryLabel);

            RebuildList();
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
                    openEvent.SetEnabled(SettingsEvent(record) != null);
                    openEvent.tooltip = SettingsEvent(record) != null ? "Select the captured event and reveal relevant settings." : "Unavailable: the captured event asset no longer exists.";
                }

                break;
            }
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

                summaryLabel.text = string.Format(
                    "{0} shown of {1} records   accepted {2}   rejected {3}",
                    visible.Count,
                    debugger.Count,
                    debugger.CountWithOutcome(PlaybackOutcome.Played),
                    debugger.Count - debugger.CountWithOutcome(PlaybackOutcome.Played));
            }

            if (emptyLabel != null)
            {
                emptyLabel.text = !AudioRuntime.Exists ? "Enter Play Mode and trigger a Sound Event in the scene." : visible.Count == 0 ? "No matching records. Trigger an event or clear the search and rejection filter." : "";
                emptyLabel.style.display = visible.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                emptyLabel.style.flexGrow = visible.Count == 0 ? 1 : 0;
                if (listView != null) listView.style.display = visible.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
                int selectedIndex = hasSelectedRecord ? visible.IndexOf(selectedRecord) : -1;
                if (selectedIndex >= 0) listView.SetSelectionWithoutNotify(new[] { selectedIndex });
                else
                {
                    hasSelectedRecord = false; selectedBanks.Clear(); UpdateBankState(); detailLabel.text = "Select a captured outcome to inspect its reason.";
                    gainLabel.text = "No record selected."; openEvent.SetEnabled(false); openEvent.tooltip = "Select a captured record with an available event asset.";
                }
            }
        }

    }
}
