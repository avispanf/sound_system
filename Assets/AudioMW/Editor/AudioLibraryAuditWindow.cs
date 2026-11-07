using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioLibraryAuditWindow : EditorWindow
    {
        [SerializeField] private string scope = "Assets";
        [SerializeField] private string query = "";
        [SerializeField] private bool errorsOnly;
        private AudioLibraryReport report;
        private readonly List<AudioLibraryIssue> visible = new List<AudioLibraryIssue>();
        private ListView list;
        private Label summary;
        private Label detail;
        private Button open;
        private Button export;
        private AudioLibraryIssue selected;

        [MenuItem("Window/AudioMW/Library Audit")]
        public static void Open()
        {
            AudioLibraryAuditWindow window = GetWindow<AudioLibraryAuditWindow>();
            window.titleContent = new GUIContent("Library Audit"); window.minSize = new Vector2(620, 360); window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.Add(AudioWorkspace.Navigation("Library Audit"));
            ObjectField folder = new ObjectField("Project folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(scope) };
            folder.RegisterValueChangedCallback(evt =>
            {
                string path = AssetDatabase.GetAssetPath(evt.newValue);
                if ((path == "Assets" || path.StartsWith("Assets/")) && AssetDatabase.IsValidFolder(path)) scope = path;
                else { folder.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(scope)); return; }
                report = null; Rebuild();
            }); rootVisualElement.Add(folder);
            Toolbar controls = new Toolbar();
            controls.Add(new ToolbarButton(Scan) { text = "Scan library", tooltip = "Read-only scan of the selected folder. Banks elsewhere under Assets are considered for membership." });
            ToolbarSearchField search = new ToolbarSearchField { value = query }; search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(evt => { query = evt.newValue; Rebuild(); }); controls.Add(search);
            ToolbarToggle errors = new ToolbarToggle { text = "Errors only", value = errorsOnly };
            errors.RegisterValueChangedCallback(evt => { errorsOnly = evt.newValue; Rebuild(); }); controls.Add(errors);
            export = new ToolbarButton(() =>
            {
                if (report == null) return;
                string path = EditorUtility.SaveFilePanel("Export library audit", "", "audiomw-library-audit.json", "json");
                if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }) { text = "Export JSON", name = "audit-export", tooltip = "Export the complete snapshot, including issues hidden by filters." };
            controls.Add(export); rootVisualElement.Add(controls);
            summary = new Label { name = "audit-summary", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingTop = 6, paddingBottom = 6 } }; rootVisualElement.Add(summary);
            list = new ListView { name = "audit-issues", itemsSource = visible, fixedItemHeight = 28, selectionType = SelectionType.Single };
            list.style.flexGrow = 1;
            list.makeItem = () => new Label();
            list.bindItem = (element, i) =>
            {
                AudioLibraryIssue issue = visible[i]; Label label = (Label)element;
                label.text = issue.Severity + " · " + issue.AssetName + " · " + issue.Rule;
                label.tooltip = issue.AssetPath + "\n" + issue.Message;
            };
            list.selectionChanged += selection =>
            {
                foreach (object item in selection)
                {
                    selected = (AudioLibraryIssue)item;
                    detail.text = selected.AssetPath + "\n" + selected.Rule + " · " + selected.Message;
                    open.SetEnabled(selected.Asset != null); if (selected.Asset != null) Selection.activeObject = selected.Asset; break;
                }
            }; rootVisualElement.Add(list);
            detail = new Label { name = "audit-detail", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingTop = 6, minHeight = 60 } }; rootVisualElement.Add(detail);
            open = new Button(() => { if (selected != null && selected.Asset != null) { Selection.activeObject = selected.Asset; EditorUtility.OpenPropertyEditor(selected.Asset); } }) { text = "Open asset settings", name = "audit-open" }; rootVisualElement.Add(open);
            Rebuild();
        }

        public void Scan()
        {
            try { report = AudioLibraryAudit.Scan(scope); Rebuild(); }
            catch (System.Exception error)
            {
                report = null; Rebuild();
                summary.text = "Scan could not complete: " + error.Message;
            }
        }

        private void Rebuild()
        {
            if (list == null) return;
            list.SetSelectionWithoutNotify(System.Array.Empty<int>());
            selected = null; visible.Clear(); open.SetEnabled(false); export.SetEnabled(report != null);
            detail.text = "Select an issue to inspect its cause. This audit never changes assets or loads Sound Banks.";
            if (report == null) summary.text = "Choose a Project folder and scan. This checks authoring structure, not listening quality or redistribution rights.";
            else
            {
                foreach (AudioLibraryIssue issue in report.Issues)
                    if ((!errorsOnly || issue.Severity == "Error") && (string.IsNullOrEmpty(query) || (issue.AssetPath + " " + issue.AssetName + " " + issue.Rule + " " + issue.Message).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)) visible.Add(issue);
                summary.text = report.AssetsScanned + " assets · " + report.Errors + " errors · " + report.Warnings + " warnings · " + visible.Count + " shown\nSnapshot · Rescan after edits. Zero issues does not imply release or listening approval.";
            }
            list.style.display = visible.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            summary.style.flexGrow = visible.Count == 0 ? 1 : 0;
            list.Rebuild();
        }
    }
}
