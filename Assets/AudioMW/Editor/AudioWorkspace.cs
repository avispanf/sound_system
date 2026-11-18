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
        private static readonly Dictionary<Object, EditorWindow> pinnedWindows = new Dictionary<Object, EditorWindow>();
        [MenuItem("Window/AudioMW/Studio", false, 0)]
        public static void Sounds() { AudioEventEditorWindow.Open(); }
        [MenuItem("Window/AudioMW/Panels/Authoring/Music", false, 1)]
        public static void Music() { MusicStatePreviewWindow.OpenMenu(); }
        [MenuItem("Window/AudioMW/Diagnostics", false, 2)]
        public static void Diagnostics() { EventDebuggerWindow.Open(); }

        public static void Style(VisualElement root)
        {
            root.AddToClassList("audiomw-surface");
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

        [MenuItem("Window/AudioMW/Mixer", false, 1)]
        public static void Mixer() { StudioMixerWindow.Open(Selection.activeObject); }

        public static Toolbar Navigation(string current)
        {
            bool diagnostics = current == "Diagnostics" || current == "Performance" || current == "Library Audit" || current == "Loudness Audit" || current == "Import Audit" || current == "Live edits";
            Toolbar bar = new Toolbar { name = "workspace-navigation" };
            bar.AddToClassList("audiomw-group-navigation");
            Add(bar, "Studio", diagnostics ? "Diagnostics" : "Studio", Sounds);
            bar.Add(new ToolbarButton(Mixer) { text = "Mixer" });
            Add(bar, "Diagnostics", diagnostics ? "Diagnostics" : "Authoring", Diagnostics);
            var sections = new ToolbarMenu { text = current == "Sounds" ? "Events" : current, name = "workspace-sections" };
            sections.AddToClassList("audiomw-section-menu");
            if (diagnostics)
            {
                sections.menu.AppendAction("Event requests", _ => Diagnostics());
                sections.menu.AppendAction("Performance", _ => AudioPerformanceWindow.Open());
                sections.menu.AppendSeparator();
                sections.menu.AppendAction("Library audit", _ => AudioLibraryAuditWindow.Open());
                sections.menu.AppendAction("Loudness audit", _ => LoudnessAuditWindow.Open());
                sections.menu.AppendAction("Import audit", _ => ImportAuditWindow.Open());
                sections.menu.AppendAction("Play Mode edits", _ => LiveTweakWindow.Open());
            }
            else
            {
                sections.menu.AppendAction("Events", _ => Sounds());
                sections.menu.AppendAction("Music", _ => Music());
                sections.menu.AppendAction("Parameters", _ => AudioInspectionWindow.Parameters());
                sections.menu.AppendAction("Routing", _ => AudioInspectionWindow.Routing());
                sections.menu.AppendAction("Spatial", _ => AudioInspectionWindow.Spatial());
                sections.menu.AppendSeparator();
                sections.menu.AppendAction("Review changes", _ => AudioAgentReviewWindow.Open());
                sections.menu.AppendAction("Project setup", _ => AudioProjectSetupWindow.Open());
                sections.menu.AppendAction("Mix setup", _ => AudioMixSetupWindow.Open());
                sections.menu.AppendAction("Create event", _ => SoundEventSetupWindow.Open());
            }
            bar.Add(sections);
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
            if (!section.value) { section.value = true; return false; }
            VisualElement field = string.IsNullOrEmpty(property) ? section : root.Q(property);
            if (field == null) return true;
            VisualElement input = field.Q<FloatField>() as VisualElement ?? field.Q<IntegerField>() as VisualElement ?? field.Q<ObjectField>();
            if (!string.IsNullOrEmpty(property) && (input == null || !(input.layout.width > 0 && input.layout.height > 0))) return false;
            field.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(field);
            input?.Focus();
            return true;
        }

        private static bool Inspects(EditorWindow window, Object asset)
        {
            SoundEventInspectorView view = window.rootVisualElement.Q<SoundEventInspectorView>();
            if (view != null && view.Asset == asset && view.panel != null) return true;
            VisualElement profile = window.rootVisualElement.Q<VisualElement>(className: "audiomw-profile-inspector");
            return profile != null && profile.panel != null && profile.userData as Object == asset;
        }

        private static EditorWindow RememberPin(Object asset, EditorWindow window)
        {
            pinnedWindows[asset] = window;
            window.Show(); window.Focus();
            return window;
        }

        public static EditorWindow Pin(Object asset)
        {
            if (asset == null) return null;
            var closed = new List<Object>();
            foreach (var entry in pinnedWindows) if (entry.Key == null || entry.Value == null) closed.Add(entry.Key);
            foreach (Object key in closed) pinnedWindows.Remove(key);
            if (pinnedWindows.TryGetValue(asset, out EditorWindow pinned) && pinned != null)
                return RememberPin(asset, pinned);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().Name != "PropertyEditor") continue;
                if (Inspects(window, asset)) return RememberPin(asset, window);
            }
            HashSet<int> existing = new HashSet<int>();
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) existing.Add(window.GetInstanceID());
            EditorUtility.OpenPropertyEditor(asset);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (!existing.Contains(window.GetInstanceID()) && window.GetType().Name == "PropertyEditor") return RememberPin(asset, window);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().Name != "PropertyEditor") continue;
                if (Inspects(window, asset)) return RememberPin(asset, window);
            }
            return null;
        }
    }
}
