using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class LoudnessAuditWindow : EditorWindow
    {
        private readonly List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>();

        private ListView listView;
        private Label summaryLabel;
        private DoubleField targetField;
        private DoubleField toleranceField;
        private Toggle outliersOnly;
        private List<LoudnessAuditEntry> visible = new List<LoudnessAuditEntry>();

        [MenuItem("Window/AudioMW/Loudness Audit")]
        public static void Open()
        {
            LoudnessAuditWindow window = GetWindow<LoudnessAuditWindow>();
            window.titleContent = new GUIContent("Loudness Audit");
            window.minSize = new Vector2(520f, 300f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.paddingLeft = 6f;
            controls.style.paddingTop = 6f;

            targetField = new DoubleField("Target LUFS");
            targetField.value = LoudnessMeter.DefaultTargetLufs;
            targetField.style.width = 160f;
            controls.Add(targetField);

            toleranceField = new DoubleField("Tolerance dB");
            toleranceField.value = LoudnessAuditReport.DefaultToleranceDb;
            toleranceField.style.width = 160f;
            controls.Add(toleranceField);

            outliersOnly = new Toggle("Outliers only");
            outliersOnly.RegisterValueChangedCallback(evt => Rebuild());
            controls.Add(outliersOnly);

            rootVisualElement.Add(controls);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
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

            rootVisualElement.Add(buttons);

            listView = new ListView();
            listView.fixedItemHeight = 20f;
            listView.style.flexGrow = 1f;
            listView.makeItem = () => new Label();
            listView.bindItem = BindItem;
            listView.itemsSource = visible;
            listView.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(listView);

            summaryLabel = new Label("nothing scanned yet");
            summaryLabel.style.paddingLeft = 6f;
            summaryLabel.style.paddingBottom = 6f;
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(summaryLabel);
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
            Label label = (Label)element;

            if (!entry.Readable)
            {
                label.text = string.Format("{0,-34} not readable, needs Decompress On Load", Truncate(entry.ClipName, 34));
                label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                return;
            }

            double offset = entry.SuggestedOffsetDb(targetField.value);

            label.text = string.Format(
                "{0,-34} {1,8:F1} LUFS   peak {2,6:F1}   offset {3,6:F1} dB",
                Truncate(entry.ClipName, 34),
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
