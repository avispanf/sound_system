using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public sealed class AudioInspectionWindow : EditorWindow
    {
        public enum Workspace { Spatial, Parameters, Routing }
        [SerializeField] private Workspace workspace;
        [SerializeField] private Object selected;
        [SerializeField] private string query = "";
        [SerializeField] private bool follow = true;
        [SerializeField] private float previewValue;
        [SerializeField] private SoundEmitter emitter;
        [SerializeField] private AudioListener listener;
        private readonly List<Object> assets = new List<Object>();
        private readonly List<Object> visible = new List<Object>();
        private ListView library;
        private ScrollView detail;
        private ScrollView contextHost;
        private VisualElement context;
        private VisualElement layout;
        private Label status;
        private int dirty;
        private int dependencyDirty;
        [MenuItem("Window/AudioMW/Spatial")]
        public static void Spatial() { Open(Workspace.Spatial); }
        [MenuItem("Window/AudioMW/Parameters")]
        public static void Parameters() { Open(Workspace.Parameters); }
        [MenuItem("Window/AudioMW/Routing")]
        public static void Routing() { Open(Workspace.Routing); }
        public static AudioInspectionWindow Open(Workspace mode)
        {
            var window = Resources.FindObjectsOfTypeAll<AudioInspectionWindow>().FirstOrDefault(w => w.workspace == mode);
            if (window == null) { window = CreateInstance<AudioInspectionWindow>(); window.workspace = mode; }
            window.titleContent = new GUIContent(mode.ToString()); window.minSize = new Vector2(620, 360); window.Show(); window.CreateGUI(); window.Focus(); return window;
        }
        private void OnEnable() { Selection.selectionChanged += Follow; Undo.undoRedoPerformed += Refresh; }
        private void OnDisable() { Selection.selectionChanged -= Follow; Undo.undoRedoPerformed -= Refresh; }
        private void OnProjectChange() { Refresh(); }
        private void Follow() { if (follow && assets.Contains(Selection.activeObject)) SelectAsset(Selection.activeObject); }
        private void OnInspectorUpdate()
        {
            int dependency = selected is SoundEvent item && item.AttenuationPreset != null ? EditorUtility.GetDirtyCount(item.AttenuationPreset) : 0;
            if (selected != null && (EditorUtility.GetDirtyCount(selected) != dirty || dependency != dependencyDirty)) BuildDetail();
            Repaint();
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement); rootVisualElement.Add(AudioWorkspace.Navigation(workspace.ToString()));
            rootVisualElement.AddToClassList("audiomw-workspace");
            var panel = new VisualElement { name = "inspection-library-panel" };
            var heading = new Label("Project browser"); heading.AddToClassList("audiomw-panel-heading"); panel.Add(heading);
            var bar = new Toolbar(); var search = new ToolbarSearchField { name = "workspace-search", value = query }; search.style.flexGrow = 1; search.style.flexShrink = 1; search.style.minWidth = 0; search.style.width = 0;
            search.RegisterValueChangedCallback(e => { query = e.newValue; Filter(); }); bar.Add(search);
            var toggle = new ToolbarToggle { text = "Follow selection", value = follow }; toggle.RegisterValueChangedCallback(e => { follow = e.newValue; Follow(); });
            bar.Add(new ToolbarButton(Refresh) { text = "Refresh" }); panel.Add(bar);
            layout = new VisualElement { name = "inspection-layout" }; rootVisualElement.Add(layout);
            var split = new TwoPaneSplitView(0, 190, TwoPaneSplitViewOrientation.Horizontal); split.style.flexGrow = 1; split.style.minWidth = 0; layout.Add(split);
            library = new ListView(visible, 24, () => { var label = new Label(); label.AddToClassList("audiomw-library-item"); return label; }, (v, i) => { var label = (Label)v; label.text = visible[i].name; label.tooltip = AssetDatabase.GetAssetPath(visible[i]); label.style.overflow = Overflow.Hidden; label.style.textOverflow = TextOverflow.Ellipsis; }) { selectionType = SelectionType.Single, name = "workspace-library" };
            library.horizontalScrollingEnabled = false;
            panel.style.minWidth = 150; library.style.flexGrow = 1;
            library.style.minWidth = 150; library.AddToClassList("audiomw-list"); library.selectionChanged += items => { foreach (Object item in items) { SelectAsset(item); Selection.activeObject = item; break; } };
            detail = new ScrollView { name = "workspace-detail" }; detail.AddToClassList("audiomw-preview"); detail.AddToClassList("audiomw-inspection"); detail.style.minWidth = 260; panel.Add(library); panel.Add(toggle); split.Add(panel); split.Add(detail);
            contextHost = new ScrollView { name = "inspection-context-host" }; layout.Add(contextHost);
            layout.RegisterCallback<GeometryChangedEvent>(_ => PlaceContext());
            status = new Label { name = "workspace-status" }; status.AddToClassList("audiomw-status"); rootVisualElement.Add(status); Refresh();
        }
        private void PlaceContext()
        {
            if (layout == null || context == null) return;
            bool narrow = layout.layout.width < 1000;
            layout.EnableInClassList("audiomw-workspace-narrow", narrow);
            if (narrow) { if (context.parent != detail.contentContainer) detail.Add(context); }
            else if (context.parent != contextHost.contentContainer) contextHost.Add(context);
        }
        private void ContextNote(string value)
        {
            var label = new Label(value); label.style.whiteSpace = WhiteSpace.Normal; label.style.marginTop = 8; context.Add(label);
        }
        public void SelectAsset(Object item)
        {
            if (selected != item) { selected = item; previewValue = item is SoundParameter p ? p.DefaultValue : 0; }
            int index = visible.IndexOf(item); if (index >= 0) library?.SetSelectionWithoutNotify(new[] { index }); BuildDetail();
        }
        private void Refresh()
        {
            if (detail == null) return;
            assets.Clear();
            var types = workspace == Workspace.Parameters ? new[] { "SoundParameter" } : workspace == Workspace.Spatial ? new[] { "SoundEvent", "AttenuationPreset" } : new[] { "SoundEvent", "MusicTrack", "VoiceLine", "MixerRoutingProfile" };
            foreach (string type in types) foreach (string guid in AssetDatabase.FindAssets("t:" + type)) { var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)); if (asset != null) assets.Add(asset); }
            assets.Sort((a,b) => string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase)); Filter();
        }
        private void Filter()
        {
            visible.Clear(); visible.AddRange(assets.Where(a => a.name.IndexOf(query,StringComparison.OrdinalIgnoreCase) >= 0)); library.Rebuild();
            if (selected == null && visible.Count > 0) SelectAsset(visible.Contains(Selection.activeObject) ? Selection.activeObject : visible[0]);
            else BuildDetail();
            int index = visible.IndexOf(selected); library.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : new int[0]);
            status.text = visible.Count + " assets · Read-only workspace · Edit with native Inspector";
        }
        private void BuildDetail()
        {
            if (detail == null) return; detail.Clear(); contextHost.Clear();
            context = new VisualElement { name = "inspection-context" };
            var heading = new Label(workspace + " context"); heading.AddToClassList("audiomw-panel-heading"); context.Add(heading);
            if (selected == null || !visible.Contains(selected))
            {
                Note(visible.Count == 0 ? "No matching assets. Reset search or create content in the Project window." : "Select an asset from the library.");
                detail.Add(new Button(() => { query = ""; rootVisualElement.Q<ToolbarSearchField>().SetValueWithoutNotify(""); Filter(); }) { text = "Reset search" }); ContextNote("Select an available asset to inspect its settings."); PlaceContext(); return;
            }
            dirty = EditorUtility.GetDirtyCount(selected); dependencyDirty = selected is SoundEvent dependencyEvent && dependencyEvent.AttenuationPreset != null ? EditorUtility.GetDirtyCount(dependencyEvent.AttenuationPreset) : 0;
            var eyebrow = new Label(workspace.ToString().ToUpperInvariant() + " / READ-ONLY OVERVIEW"); eyebrow.AddToClassList("audiomw-eyebrow"); detail.Add(eyebrow);
            var title = new Label(selected.name); title.AddToClassList("audiomw-title"); detail.Add(title);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = Wrap.Wrap;
            actions.Add(new Button(() => Selection.activeObject = selected) { text = "Show settings" }); actions.Add(new Button(() => AudioWorkspace.Pin(selected)) { text = "Pin native Inspector" }); context.Add(actions);
            ContextNote("Settings are edited in the native Inspector, with Undo and multi-edit.");
            if (workspace == Workspace.Spatial) DrawSpatial(); else if (workspace == Workspace.Parameters) DrawParameter(); else DrawRouting();
            PlaceContext();
        }
        private void Note(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; label.style.marginTop = 6; detail.Add(label); }
        private void Link(Object asset, string text) { var button = new Button(() => Selection.activeObject = asset) { text = text, tooltip = asset != null ? AssetDatabase.GetAssetPath(asset) : "Reference is missing" }; button.SetEnabled(asset != null); detail.Add(button); }
        private void DrawSpatial()
        {
            var item = selected as SoundEvent; var preset = selected as AttenuationPreset ?? (item != null ? item.AttenuationPreset : null);
            if (item != null && AudioInspectionData.IsParent(item)) { Note("This container has no single attenuation curve. Each leaf owns its spatial settings."); foreach (var child in item.Children) if (child != null) Link(child.Event, child.Event != null ? child.Event.name : "Missing child"); return; }
            if (preset != null) Link(preset, "Preset: " + preset.name + " · overrides event spatial settings"); else Note("Event spatial settings · no preset override");
            float min = preset != null ? preset.MinDistance : item.MinDistance, max = preset != null ? preset.MaxDistance : item.MaxDistance;
            float blend = preset != null ? preset.SpatialBlend : item.SpatialBlend; var mode = preset != null ? preset.RolloffMode : item.RolloffMode;
            ContextNote($"{mode} · Near {min:0.##} m · Far {max:0.##} m · 3D blend {blend:0.##}");
            ContextNote("Distance model, not measured output. Scale assumes 1 Unity unit = 1 metre. Mixer, occlusion, spread and spatializer are separate.");
            var source = new ObjectField("Emitter") { objectType = typeof(SoundEmitter), allowSceneObjects = true, value = emitter };
            var ears = new ObjectField("Listener") { objectType = typeof(AudioListener), allowSceneObjects = true, value = listener }; context.Add(source); context.Add(ears);
            source.RegisterValueChangedCallback(e => emitter = e.newValue as SoundEmitter); ears.RegisterValueChangedCallback(e => listener = e.newValue as AudioListener);
            var distance = new Slider("Preview distance (m)", 0, Mathf.Max(.01f,max)) { value = Mathf.Clamp(previewValue,0,max), showInputField = true }; distance.RegisterValueChangedCallback(e => previewValue = e.newValue);
            var result = new Label { name = "spatial-context" }; result.style.whiteSpace = WhiteSpace.Normal;
            Func<float> sampleDistance = () =>
            {
                bool matching = emitter != null && listener != null && emitter.gameObject.scene.IsValid() && listener.gameObject.scene.IsValid() && emitter.isActiveAndEnabled && listener.isActiveAndEnabled && (item != null ? emitter.Event == item : emitter.Event != null && emitter.Event.AttenuationPreset == preset);
                float d = matching ? Vector3.Distance(emitter.transform.position,listener.transform.position) : previewValue;
                result.text = matching ? $"Scene distance: {d:0.##} m · {emitter.name} → {listener.name}" : "Preview marker only · choose a matching active scene emitter and listener for real distance.";
                return d;
            };
            result.schedule.Execute(() => sampleDistance()).Every(100);
            bool known = mode != AudioRolloffMode.Custom || preset != null && preset.UsesCustomCurve;
            if (!known) Note("Curve unavailable: Custom mode has no authored preset curve. Inspect the actual runtime AudioSource.");
            else detail.Add(new IMGUIContainer(() =>
            {
                if (selected == null) return;
                float d = sampleDistance();
                AudioResponseGraph.Draw(max, d, x => AudioInspectionData.DistanceModel(x,min,max,blend,mode,preset != null ? preset.CustomRolloff : null), "Distance (m)", "Amplitude (0–1)");
            }));
            detail.Add(distance); detail.Add(result);
            if (item != null) { context.Add(new Button(() => AudioWorkspace.OpenEvent(item,"Spatial audio")) { text = "Open spatial settings" }); detail.Add(new AudioEventContextPane(item)); }
            ContextNote("Reverb and echo remain in the native AudioMixer / scene components. Selecting an emitter in Scene View uses its existing spherical distance gizmos; no invented shape is drawn here.");
        }
        private void DrawParameter()
        {
            var parameter = (SoundParameter)selected;
            ContextNote($"Range {parameter.MinValue:0.###}…{parameter.MaxValue:0.###} · Default {parameter.DefaultValue:0.###} · Authored scope: {(parameter.IsGlobal ? "global" : "local")}");
            var slider = new Slider("Preview value",parameter.MinValue,parameter.MaxValue) { value = parameter.Clamp(previewValue), showInputField = true }; detail.Add(slider); slider.RegisterValueChangedCallback(e => previewValue = e.newValue);
            ContextNote("Preview changes only this view. It never writes the asset default or game parameters. Global store and emitter-local overrides are different owners.");
            var live = new Label { name = "parameter-runtime" }; live.style.whiteSpace = WhiteSpace.Normal; context.Add(live);
            var refs = AudioInspectionData.ParameterUses(parameter); Note(refs.Count + " authored references · project assets only; dynamic code references are unavailable");
            foreach (var use in refs)
            {
                Link(use.Asset,use.Label);
                if (use.Evaluate != null)
                {
                    var foldout = new Foldout { text = "Response · " + use.Unit, value = false };
                    foldout.Add(new IMGUIContainer(() => { if (parameter != null) AudioResponseGraph.Draw(parameter.MaxValue, previewValue, use.Evaluate, "Parameter value",use.Unit,parameter.MinValue); })); detail.Add(foldout);
                }
            }
            Action update = () =>
            {
                if (parameter == null) return;
                if (!EditorApplication.isPlaying || !AudioRuntime.Exists) live.text = "Runtime unavailable: enter Play Mode with an AudioMW runtime.";
                else { bool set = AudioRuntime.Instance.GlobalParameters.TryGet(parameter,out float value); live.text = set ? "Global store: " + value.ToString("0.###") + " · local overrides unavailable here" : "Global store: unset · fallback default " + parameter.DefaultValue.ToString("0.###") + " · local overrides unavailable here"; }
            }; update(); live.schedule.Execute(update).Every(250);
        }
        private void DrawRouting()
        {
            Note("All authored branches are shown. Switch states are evaluated at trigger; this is not a list of currently playing voices.");
            var routes = AudioInspectionData.Routes(selected); foreach (var route in routes)
            {
                var card = new VisualElement(); card.AddToClassList("audiomw-route-card"); detail.Add(card);
                card.Add(new Label(route.Path) { style = { whiteSpace = WhiteSpace.Normal }, tooltip = route.Path });
                if (route.Group != null)
                {
                    card.Add(new Button(() => Selection.activeObject = route.Group) { text = "Select output: " + route.Group.name });
                    card.Add(new Button(() => AssetDatabase.OpenAsset(route.Group.audioMixer)) { text = "Open native AudioMixer" });
                }
                if (route.Owner != null && route.Owner != selected) card.Add(new Button(() => Selection.activeObject = route.Owner) { text = "Open routed asset: " + route.Owner.name });
            }
            if (selected is MixerRoutingProfile profile)
            {
                Link(profile.Mixer, profile.Mixer != null ? profile.Mixer.name : "Missing mixer");
                foreach (var binding in profile.Bindings)
                {
                    if (binding == null || !binding.IsValid) { Note("Invalid parameter binding · inspect routing profile"); continue; }
                    Link(binding.Parameter,binding.Parameter.name + " → " + binding.ExposedName);
                    bool available = profile.Mixer != null && profile.Mixer.GetFloat(binding.ExposedName,out _);
                    Note($"Output range {binding.OutputMin:0.##}…{binding.OutputMax:0.##} · {(available ? "Exposed control found" : "Unavailable: exposed control or mixer missing")} · units belong to the native mixer control");
                }
            }
            ContextNote("Shown: authored output assignments. Parent group topology, effect sends, snapshots and live bus levels are unavailable in this view; inspect the native mixer. No Master connection or meter is inferred.");
            context.Add(new Button(AudioMixSetupWindow.Open) { text = "Review mix setup", tooltip = "Open the separate confirmation workflow. No changes are applied by opening it." });
        }
    }
}
