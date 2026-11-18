using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class LoudnessAuditWindow : EditorWindow
    {
        [MenuItem("Window/AudioMW/Panels/Diagnostics/Loudness Audit")]
        public static void Open() => AudioEventEditorWindow.OpenDock("Loudness audit");

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.Add(new StudioLoudnessAudit());
        }
    }

    internal sealed class StudioLoudnessAudit : VisualElement
    {
        private readonly List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>();

        private ListView listView;
        private Label summaryLabel;
        private DoubleField targetField;
        private DoubleField toleranceField;
        private Toggle outliersOnly;
        private List<LoudnessAuditEntry> visible = new List<LoudnessAuditEntry>();

        internal StudioLoudnessAudit()
        {
            style.flexGrow = 1;
            AddToClassList("audiomw-audit");
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.AddToClassList("audiomw-audit-controls");
            controls.style.paddingLeft = 6f;
            controls.style.paddingTop = 6f;

            targetField = new DoubleField("Target LUFS");
            targetField.value = LoudnessMeter.DefaultTargetLufs;
            targetField.style.width = 180f; targetField.labelElement.style.minWidth = 90;
            controls.Add(targetField);

            toleranceField = new DoubleField("Tolerance dB");
            toleranceField.value = LoudnessAuditReport.DefaultToleranceDb;
            toleranceField.style.width = 180f; toleranceField.labelElement.style.minWidth = 90;
            controls.Add(toleranceField);

            outliersOnly = new Toggle("Outliers only");
            outliersOnly.RegisterValueChangedCallback(evt => Rebuild());
            controls.Add(outliersOnly);

            Add(controls);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.AddToClassList("audiomw-audit-controls");
            buttons.style.paddingLeft = 6f;

            Button scanAll = new Button(() => Scan(false));
            scanAll.text = "Scan project";
            buttons.Add(scanAll);

            Button scanSelection = new Button(() => Scan(true));
            scanSelection.text = "Scan selection";
            buttons.Add(scanSelection);

            Button export = new Button(ExportCsv);
            export.text = "Export CSV";
            buttons.Add(export);

            Add(buttons);

            listView = new ListView();
            listView.fixedItemHeight = 46f;
            listView.horizontalScrollingEnabled = false;
            listView.style.flexGrow = 1f;
            listView.makeItem = () => { var row = new VisualElement(); row.Add(new Label { name = "audit-name" }); row.Add(new Label { name = "audit-details" }); return row; };
            listView.bindItem = BindItem;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            Add(listView);

            summaryLabel = new Label("No scan yet · choose Scan project or select clips first.");
            summaryLabel.style.paddingLeft = 6f;
            summaryLabel.style.paddingBottom = 6f;
            summaryLabel.style.paddingRight = 12f;
            summaryLabel.style.flexShrink = 0;
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(summaryLabel);
        }

        private void Scan(bool selectionOnly)
        {
            entries.Clear();

            List<AudioClip> clips = new List<AudioClip>();

            if (selectionOnly)
            {
                clips.AddRange(Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets));
            }
            else
            {
                string[] guids = AssetDatabase.FindAssets("t:AudioClip");

                for (int i = 0; i < guids.Length; i++)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[i]));

                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }
            }

            try
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    AudioClip clip = clips[i];

                    if (EditorUtility.DisplayCancelableProgressBar("Loudness audit", clip.name, (float)i / Mathf.Max(1, clips.Count)))
                    {
                        break;
                    }

                    entries.Add(Analyze(clip));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Rebuild();
        }

        private static LoudnessAuditEntry Analyze(AudioClip clip)
        {
            LoudnessAuditEntry entry = new LoudnessAuditEntry();
            entry.ClipName = clip.name;
            entry.AssetPath = AssetDatabase.GetAssetPath(clip);

            float[] buffer = new float[clip.samples * clip.channels];
            bool readable = clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.GetData(buffer, 0);

            entry.Readable = readable;
            entry.Loudness = readable
                ? LoudnessMeter.Analyze(buffer, clip.channels, clip.frequency)
                : LoudnessResult.Silent(clip.length);

            return entry;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= visible.Count)
            {
                return;
            }

            LoudnessAuditEntry entry = visible[index];
            element.Q<Label>("audit-name").text = entry.ClipName; element.tooltip = entry.ClipName;
            Label label = element.Q<Label>("audit-details");

            if (!entry.Readable)
            {
                label.text = "Unavailable · needs Decompress On Load";
                label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                return;
            }

            double offset = entry.SuggestedOffsetDb(targetField.value);

            label.text = string.Format(
                "{0:F1} LUFS · peak {1:F1} dBTP · offset {2:F1} dB",
                entry.Loudness.IntegratedLufs,
                entry.Loudness.TruePeakDb,
                offset);

            label.style.color = Mathf.Abs((float)offset) > (float)toleranceField.value
                ? new StyleColor(new Color(1f, 0.7f, 0.55f))
                : new StyleColor(new Color(0.75f, 0.85f, 0.75f));
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is LoudnessAuditEntry)
                {
                    LoudnessAuditEntry entry = (LoudnessAuditEntry)item;
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);

                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                }

                break;
            }
        }

        private void Rebuild()
        {
            visible = outliersOnly != null && outliersOnly.value
                ? LoudnessAuditReport.FindOutliers(entries, targetField.value, toleranceField.value)
                : new List<LoudnessAuditEntry>(entries);

            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
            }

            if (summaryLabel != null)
            {
                double average = LoudnessAuditReport.AverageLoudness(entries);
                int clipping = LoudnessAuditReport.CountClipping(entries, -1.0);
                int outliers = LoudnessAuditReport.FindOutliers(entries, targetField.value, toleranceField.value).Count;

                summaryLabel.text = string.Format(
                    "{0} clips scanned   average {1:F1} LUFS   {2} outside tolerance   {3} above -1 dBTP",
                    entries.Count,
                    average,
                    outliers,
                    clipping);
            }
        }

        private void ExportCsv()
        {
            if (entries.Count == 0)
            {
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export loudness audit", "", "loudness-audit.csv", "csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            System.IO.File.WriteAllText(path, LoudnessAuditReport.ToCsv(entries, targetField.value));
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
