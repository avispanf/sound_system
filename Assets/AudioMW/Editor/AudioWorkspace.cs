using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public static class AudioWorkspace
    {
        [MenuItem("Window/AudioMW/Sounds", false, 0)]
        public static void Sounds() { AudioBrowserWindow.Open(); }
        [MenuItem("Window/AudioMW/Music", false, 1)]
        public static void Music() { MusicStatePreviewWindow.OpenMenu(); }
        [MenuItem("Window/AudioMW/Diagnostics", false, 2)]
        public static void Diagnostics() { EventDebuggerWindow.Open(); }

        public static void Style(VisualElement root)
        {
            string[] scripts = AssetDatabase.FindAssets("AudioWorkspace t:MonoScript");
            if (scripts.Length == 0) return;
            string path = System.IO.Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(scripts[0])).Replace('\\', '/') + "/UI/AudioMWStyles.uss";
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }

        [Shortcut("AudioMW/Find in Sounds", typeof(AudioBrowserWindow), KeyCode.F, ShortcutModifiers.Action)]
        private static void FindSounds(ShortcutArguments args) { FocusSearch(args.context as EditorWindow); }

        [Shortcut("AudioMW/Find in Diagnostics", typeof(EventDebuggerWindow), KeyCode.F, ShortcutModifiers.Action)]
        private static void FindDiagnostics(ShortcutArguments args) { FocusSearch(args.context as EditorWindow); }

        private static void FocusSearch(EditorWindow window)
        {
            ToolbarSearchField search = window != null ? window.rootVisualElement.Q<ToolbarSearchField>() : null;
            if (search == null) return;
            TextField input = search.Q<TextField>();
            if (input != null) { input.Focus(); input.SelectAll(); }
            else search.Focus();
        }

        public static Toolbar Navigation(string current)
        {
            Toolbar bar = new Toolbar();
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.height = StyleKeyword.Auto;
            bar.name = "workspace-navigation";
            Add(bar, "Events", current == "Sounds" ? "Events" : current, Sounds);
            Add(bar, "Music", current, Music);
            Add(bar, "Parameters", current, AudioInspectionWindow.Parameters);
            Add(bar, "Routing", current, AudioInspectionWindow.Routing);
            Add(bar, "Spatial", current, AudioInspectionWindow.Spatial);
            Add(bar, "Diagnostics", current, Diagnostics);
            ToolbarSpacer space = new ToolbarSpacer { flex = true }; bar.Add(space);
            ToolbarButton create = new ToolbarButton(SoundEventSetupWindow.Open) { text = "+ Create event", tooltip = "Create a Sound Event from clips selected in the Project window." };
            create.AddToClassList("audiomw-primary"); bar.Add(create);
            ToolbarButton review = new ToolbarButton(AudioAgentReviewWindow.Open) { text = "Review changes", name = "review-changes", tooltip = "Review agent proposals. Opening this tool applies nothing." }; bar.Add(review);
            ToolbarMenu tools = new ToolbarMenu { text = "Tools" };
            tools.menu.AppendAction("Mix setup", _ => AudioMixSetupWindow.Open());
            tools.menu.AppendAction("Project setup", _ => AudioProjectSetupWindow.Open());
            tools.menu.AppendAction("Agent review", _ => AudioAgentReviewWindow.Open());
            tools.menu.AppendAction("Library audit", _ => AudioLibraryAuditWindow.Open());
            tools.menu.AppendAction("Loudness audit", _ => LoudnessAuditWindow.Open());
            tools.menu.AppendAction("Import audit", _ => ImportAuditWindow.Open());
            tools.menu.AppendAction("Review Play Mode changes", _ => LiveTweakWindow.Open());
            bar.Add(tools);
            return bar;
        }

        private static AudioBrowserWindow GetBrowser() { return EditorWindow.GetWindow<AudioBrowserWindow>(); }

        private static void Add(Toolbar bar, string title, string current, System.Action action)
        {
            ToolbarButton button = new ToolbarButton(action) { text = title };
            button.EnableInClassList("audiomw-nav-current", title == current);
            button.SetEnabled(title != current);
            bar.Add(button);
        }

        public static void OpenEvent(SoundEvent item, string section, string property = null)
        {
            if (item == null) return;
            Selection.activeObject = item;
            EditorWindow editor = Pin(item);
            if (editor == null) return;
            VisualElement currentRoot = editor.rootVisualElement;
            if (currentRoot.Q<SoundEventInspectorView>() != null && RevealSection(currentRoot, section, property)) return;
            bool complete = false;
            editor.Repaint();
            editor.rootVisualElement.schedule.Execute(() =>
            {
                VisualElement root = editor.rootVisualElement;
                if (root.Q<SoundEventInspectorView>() != null) complete = RevealSection(root, section, property);
            }).Every(16).Until(() => complete || editor == null || item == null);
        }

        public static bool RevealSection(VisualElement root, string sectionName, string property)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                ScrollView scroll = root.Q<ScrollView>();
                if (scroll != null) scroll.scrollOffset = Vector2.zero;
                return true;
            }
            Foldout section = root.Q<Foldout>("section-" + sectionName);
            if (section == null) return true;
            Toggle header = section.Q<Toggle>();
            if (section.panel == null || header == null || !(header.layout.width > 0 && header.layout.height > 0)) return false;
            section.value = true;
            VisualElement field = string.IsNullOrEmpty(property) ? section : root.Q(property);
            if (field == null) return true;
            VisualElement input = field.Q<FloatField>() as VisualElement ?? field.Q<IntegerField>() as VisualElement ?? field.Q<ObjectField>();
            if (!string.IsNullOrEmpty(property) && (input == null || !(input.layout.width > 0 && input.layout.height > 0))) return false;
            field.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(field);
            input?.Focus();
            return true;
        }

        public static EditorWindow Pin(Object asset)
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().Name != "PropertyEditor") continue;
                SoundEventInspectorView view = window.rootVisualElement.Q<SoundEventInspectorView>();
                if (view != null && view.Asset == asset && view.panel != null) { window.Show(); window.Focus(); return window; }
            }
            HashSet<int> existing = new HashSet<int>();
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) existing.Add(window.GetInstanceID());
            EditorUtility.OpenPropertyEditor(asset);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (!existing.Contains(window.GetInstanceID()) && window.GetType().Name == "PropertyEditor") return window;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().Name != "PropertyEditor") continue;
                SoundEventInspectorView view = window.rootVisualElement.Q<SoundEventInspectorView>();
                if (view != null && view.Asset == asset) { window.Focus(); return window; }
            }
            EditorWindow focused = EditorWindow.focusedWindow;
            return focused != null && focused.GetType().Name == "PropertyEditor" ? focused : null;
        }
    }
}
