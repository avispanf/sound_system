using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class ImportAuditWindow : EditorWindow
    {
        private struct Row
        {
            public ClipImportInfo Info;
            public List<ImportIssue> Issues;
        }

        private readonly List<Row> rows = new List<Row>();

        private ListView listView;
        private Label summaryLabel;
        private Toggle issuesOnly;
        private List<Row> visible = new List<Row>();

        [MenuItem("Window/AudioMW/Import Auditor")]
        public static void Open()
        {
            ImportAuditWindow window = GetWindow<ImportAuditWindow>();
            window.titleContent = new GUIContent("Import Auditor");
            window.minSize = new Vector2(560f, 300f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.paddingLeft = 6f;
            controls.style.paddingTop = 6f;

            Button scan = new Button(Scan);
            scan.text = "Scan project";
            controls.Add(scan);

            Button fixAll = new Button(FixAll);
            fixAll.text = "Fix load types";
            controls.Add(fixAll);

            issuesOnly = new Toggle("Issues only");
            issuesOnly.value = true;
            issuesOnly.RegisterValueChangedCallback(evt => Rebuild());
            controls.Add(issuesOnly);

            rootVisualElement.Add(controls);

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

        private void Scan()
        {
            rows.Clear();

            string[] guids = AssetDatabase.FindAssets("t:AudioClip");

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;

                    if (clip == null || importer == null)
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar("Import audit", clip.name, (float)i / Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    ClipImportInfo info = Describe(clip, importer, path);

                    rows.Add(new Row
                    {
                        Info = info,
                        Issues = ImportAuditRules.Evaluate(info)
                    });
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Rebuild();
        }

        private static ClipImportInfo Describe(AudioClip clip, AudioImporter importer, string path)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            ClipImportInfo info = new ClipImportInfo();
            info.Name = clip.name;
            info.AssetPath = path;
            info.LengthSeconds = clip.length;
            info.Channels = clip.channels;
            info.Frequency = clip.frequency;
            info.Samples = clip.samples;
            info.LoadType = settings.loadType.ToString();
            info.CompressionFormat = settings.compressionFormat.ToString();
            info.ForceToMono = importer.forceToMono;
            info.PreloadAudioData = clip.preloadAudioData;
            info.LoadInBackground = importer.loadInBackground;
            info.UsedSpatially = clip.channels > 1;

            return info;
        }

        private void FixAll()
        {
            int fixedCount = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                string suggested = ImportAuditRules.SuggestLoadType(row.Info.LengthSeconds);

                if (row.Info.LoadType == suggested)
                {
                    continue;
                }

                AudioImporter importer = AssetImporter.GetAtPath(row.Info.AssetPath) as AudioImporter;

                if (importer == null)
                {
                    continue;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = (AudioClipLoadType)System.Enum.Parse(typeof(AudioClipLoadType), suggested);
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                Debug.Log("AudioMW import auditor updated load type on " + fixedCount + " clip(s).");
                Scan();
            }
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= visible.Count)
            {
                return;
            }

            Row row = visible[index];
            Label label = (Label)element;

            string issueText = row.Issues.Count == 0
                ? "ok"
                : row.Issues[0].Message + (row.Issues.Count > 1 ? "  (+" + (row.Issues.Count - 1) + " more)" : string.Empty);

            label.text = string.Format(
                "{0,-28} {1,6:F1}s  {2,-18} {3,7}  {4}",
                Truncate(row.Info.Name, 28),
                row.Info.LengthSeconds,
                row.Info.LoadType,
                FormatBytes(row.Info.EstimatedMemoryBytes),
                issueText);

            label.style.color = row.Issues.Count == 0
                ? new StyleColor(new Color(0.75f, 0.85f, 0.75f))
                : new StyleColor(new Color(1f, 0.7f, 0.55f));
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is Row)
                {
                    Row row = (Row)item;
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(row.Info.AssetPath);

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
            visible = new List<Row>();

            for (int i = 0; i < rows.Count; i++)
            {
                if (issuesOnly != null && issuesOnly.value && rows[i].Issues.Count == 0)
                {
                    continue;
                }

                visible.Add(rows[i]);
            }

            if (listView != null)
            {
                listView.itemsSource = visible;
                listView.Rebuild();
            }

            if (summaryLabel != null)
            {
                List<ClipImportInfo> infos = new List<ClipImportInfo>();
                int withIssues = 0;

                for (int i = 0; i < rows.Count; i++)
                {
                    infos.Add(rows[i].Info);

                    if (rows[i].Issues.Count > 0)
                    {
                        withIssues++;
                    }
                }

                summaryLabel.text = string.Format(
                    "{0} clips scanned   {1} with issues   estimated memory {2}",
                    rows.Count,
                    withIssues,
                    FormatBytes(ImportAuditRules.TotalEstimatedMemory(infos)));
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0L)
            {
                return "0 B";
            }

            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024f).ToString("F0") + " KB";
            }

            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
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
