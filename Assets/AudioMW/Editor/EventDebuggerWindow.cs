using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class EventDebuggerWindow : EditorWindow
    {
        private ListView listView;
        private ToolbarSearchField searchField;
        private ToolbarToggle rejectionsToggle;
        private Label summaryLabel;
        private Label detailLabel;

        private List<EventDebugRecord> visible = new List<EventDebugRecord>();

        [MenuItem("Window/AudioMW/Event Debugger")]
        public static void Open()
        {
            EventDebuggerWindow window = GetWindow<EventDebuggerWindow>();
            window.titleContent = new GUIContent("Event Debugger");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        public void CreateGUI()
        {
            Toolbar toolbar = new Toolbar();

            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1f;
            searchField.RegisterValueChangedCallback(evt => RebuildList());
            toolbar.Add(searchField);

            rejectionsToggle = new ToolbarToggle();
            rejectionsToggle.text = "Rejections only";
            rejectionsToggle.RegisterValueChangedCallback(evt => RebuildList());
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
            listView.fixedItemHeight = 20f;
            listView.selectionType = SelectionType.Single;
            listView.style.flexGrow = 1f;
            listView.makeItem = () => new Label();
            listView.bindItem = BindItem;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(listView);

            detailLabel = new Label();
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailLabel.style.paddingLeft = 6f;
            detailLabel.style.paddingTop = 4f;
            detailLabel.style.minHeight = 52f;
            rootVisualElement.Add(detailLabel);

            summaryLabel = new Label();
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
            }
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= visible.Count)
            {
                return;
            }

            EventDebugRecord record = visible[index];
            Label label = (Label)element;

            label.text = string.Format(
                "{0,8:F2}  {1,-28} {2}",
                record.Time,
                Truncate(record.EventName, 28),
                record.Outcome == PlaybackOutcome.Played ? "played" : record.DescribeOutcome());

            label.style.color = record.Outcome == PlaybackOutcome.Played
                ? new StyleColor(new Color(0.75f, 0.85f, 0.75f))
                : new StyleColor(new Color(1f, 0.7f, 0.55f));
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is EventDebugRecord)
                {
                    EventDebugRecord record = (EventDebugRecord)item;

                    detailLabel.text = string.Format(
                        "{0}\nclip: {1}\noutcome: {2}\nvolume: {3}\npitch: {4:F2}\nposition: {5}  attached: {6}",
                        record.EventName,
                        string.IsNullOrEmpty(record.ClipName) ? "-" : record.ClipName,
                        record.DescribeOutcome(),
                        record.DescribeVolumeChain(),
                        record.FinalPitch,
                        record.Position,
                        record.Attached);
                }

                break;
            }
        }

        private void RebuildList()
        {
            if (!AudioRuntime.Exists)
            {
                visible = new List<EventDebugRecord>();
                summaryLabel.text = "runtime not started";
            }
            else
            {
                EventDebugger debugger = AudioRuntime.Instance.Debugger;
                visible = debugger.Filter(searchField != null ? searchField.value : null,
                    rejectionsToggle != null && rejectionsToggle.value);

                summaryLabel.text = string.Format(
                    "{0} shown of {1} records   played {2}   rejected {3}",
                    visible.Count,
                    debugger.Count,
                    debugger.CountWithOutcome(PlaybackOutcome.Played),
                    debugger.Count - debugger.CountWithOutcome(PlaybackOutcome.Played));
            }

            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
            }
        }

        private static string Truncate(string value, int length)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "(unnamed)";
            }

            return value.Length <= length ? value : value.Substring(0, length - 1) + "~";
        }
    }
}
