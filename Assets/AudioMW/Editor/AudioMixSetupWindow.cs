using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public sealed class AudioMixSetupWindow : EditorWindow
    {
        [SerializeField] private AudioMixRequest draft = new AudioMixRequest();
        private AudioMixPlan plan;
        private VisualElement review;
        private HelpBox status;
        private Button apply;
        private ListView routes;
        [MenuItem("Window/AudioMW/Mix Setup")]
        public static void Open()
        {
            AudioMixSetupWindow window = GetWindow<AudioMixSetupWindow>(); window.titleContent = new GUIContent("Mix Setup"); window.minSize = new Vector2(420, 460); window.Show();
        }
        public void LoadDraft(AudioMixRequest request)
        {
            draft = JsonUtility.FromJson<AudioMixRequest>(JsonUtility.ToJson(request)); plan = null; CreateGUI();
        }
        public void CreateGUI()
        {
            if (draft == null) draft = new AudioMixRequest();
            plan = null; rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            ScrollView scroll = new ScrollView(); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            scroll.Add(Wrap("Routing and VO ducking"));
            scroll.Add(Wrap("Choose explicit asset categories, then review exact changes. Nothing is written until Approve and apply. Existing mixer graphs and category faders are preserved."));
            Reference<AudioProjectSetup>(scroll, "Saved project setup", draft.Setup, value => draft.Setup = value);
            scroll.Add(new Button(Scan) { text = "Read routable assets", name = "mix-scan" });
            TextField folder = new TextField("Output folder") { value = draft.Folder }; folder.RegisterValueChangedCallback(evt => { draft.Folder = evt.newValue; Invalidate(); }); scroll.Add(folder);
            TextField name = new TextField("Output name") { value = draft.Name }; name.RegisterValueChangedCallback(evt => { draft.Name = evt.newValue; Invalidate(); }); scroll.Add(name);
            Reference<AudioMixer>(scroll, "Reuse compatible mixer", draft.ExistingMixer, value => draft.ExistingMixer = value);
            scroll.Add(Wrap("Leave mixer empty to copy the standard template. Reuse requires its named category/duck groups and exposed duck parameters; no groups are inserted into an existing graph."));
            Reference<SoundParameter>(scroll, "Existing VO duck parameter", draft.DuckParameter, value => draft.DuckParameter = value);
            Reference<GameObject>(scroll, "Scene target for mix scope", draft.SceneTarget, value => draft.SceneTarget = value, true);
            Float(scroll, "Music duck dB", draft.MusicDuckDb, value => draft.MusicDuckDb = value);
            Float(scroll, "Ambience duck dB", draft.AmbienceDuckDb, value => draft.AmbienceDuckDb = value);
            Float(scroll, "Attack seconds", draft.AttackSeconds, value => draft.AttackSeconds = value);
            Float(scroll, "Release seconds", draft.ReleaseSeconds, value => draft.ReleaseSeconds = value);
            Toggle replace = new Toggle("Allow replacing existing routes shown in review") { value = draft.AllowReroute };
            replace.RegisterValueChangedCallback(evt => { draft.AllowReroute = evt.newValue; Invalidate(); }); scroll.Add(replace);
            scroll.Add(Wrap("Unchanged leaves an asset untouched. Removing a source duck binding removes only Volume bindings for the selected existing VO duck parameter, preventing a second attenuation stage."));
            scroll.Add(new Button(() =>
            {
                foreach (AudioRouteChoice choice in draft.Routes) if (choice.Asset is SoundEvent && choice.Bus == AudioMixBus.Unchanged) choice.Bus = AudioMixBus.SFX;
                Invalidate(); routes.RefreshItems();
            }) { text = "Set unchanged Sound Events to SFX (draft only)" });
            routes = new ListView { itemsSource = draft.Routes, fixedItemHeight = 50, selectionType = SelectionType.None, name = "mix-routes" }; routes.style.height = 250;
            routes.makeItem = () =>
            {
                VisualElement row = new VisualElement();
                VisualElement line = new VisualElement(); line.style.flexDirection = FlexDirection.Row;
                Label label = new Label { name = "asset" }; label.style.flexGrow = 1; label.style.overflow = Overflow.Hidden;
                EnumField bus = new EnumField(AudioMixBus.Unchanged) { name = "bus" }; bus.style.width = 110;
                bus.RegisterValueChangedCallback(evt => { if (row.userData is AudioRouteChoice choice) { choice.Bus = (AudioMixBus)evt.newValue; Invalidate(); } });
                line.Add(label); line.Add(bus); row.Add(line);
                Toggle remove = new Toggle("Remove source duck Volume binding") { name = "remove" };
                remove.RegisterValueChangedCallback(evt => { if (row.userData is AudioRouteChoice choice) { choice.RemoveSourceDuck = evt.newValue; Invalidate(); } }); row.Add(remove); return row;
            };
            routes.bindItem = (element, index) =>
            {
                AudioRouteChoice choice = draft.Routes[index]; element.userData = choice;
                Label label = element.Q<Label>("asset"); label.text = choice.Asset != null ? choice.Asset.name : "Missing asset"; label.tooltip = AssetDatabase.GetAssetPath(choice.Asset);
                element.Q<EnumField>("bus").SetValueWithoutNotify(choice.Bus);
                Toggle remove = element.Q<Toggle>("remove"); remove.SetValueWithoutNotify(choice.RemoveSourceDuck); remove.SetEnabled(choice.Asset is SoundEvent && draft.DuckParameter != null);
            }; scroll.Add(routes);
            scroll.Add(new Button(Prepare) { text = "Prepare routing review", name = "mix-prepare" });
            status = new HelpBox("Draft only. Choose a saved setup and scene target, then review before applying.", HelpBoxMessageType.Info); scroll.Add(status);
            review = new VisualElement(); scroll.Add(review);
            apply = new Button(Apply) { text = "Approve and apply", name = "mix-approve" }; apply.SetEnabled(false); scroll.Add(apply);
            scroll.Add(new Button(Reject) { text = "Reject", name = "mix-reject" });
        }
        private static Label Wrap(string text) => new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6, marginTop = 4 } };
        private void Reference<T>(VisualElement parent, string label, T value, Action<T> set, bool scene = false) where T : Object
        {
            ObjectField field = new ObjectField(label) { objectType = typeof(T), allowSceneObjects = scene, value = value };
            field.RegisterValueChangedCallback(evt => { set(evt.newValue as T); Invalidate(); routes?.RefreshItems(); }); parent.Add(field);
        }
        private void Float(VisualElement parent, string label, float value, Action<float> set)
        {
            FloatField field = new FloatField(label) { value = value }; field.RegisterValueChangedCallback(evt => { set(evt.newValue); Invalidate(); }); parent.Add(field);
        }
        private void Invalidate()
        {
            plan = null; review?.Clear(); apply?.SetEnabled(false);
            if (status != null) { status.messageType = HelpBoxMessageType.Info; status.text = "Draft changed. Prepare a new review. No asset changes applied."; }
        }
        public void Reject() { Invalidate(); status.text = "Rejected. No mixer, routing, ducking or scene changes applied."; }
        public void Scan()
        {
            Invalidate();
            try
            {
                if (draft.Setup == null) throw new InvalidOperationException("Choose a saved project setup.");
                string folder = draft.Setup.Brief.SourceFolder;
                if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("The setup content folder no longer exists.");
                HashSet<Object> found = new HashSet<Object>();
                foreach (string filter in new[] { "t:SoundEvent", "t:MusicTrack", "t:VoiceLine" })
                    foreach (string guid in AssetDatabase.FindAssets(filter, new[] { folder }))
                        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))) if (asset is SoundEvent || asset is MusicTrack || asset is VoiceLine) found.Add(asset);
                draft.Routes = found.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal).ThenBy(asset => asset.name, StringComparer.Ordinal).Select(asset => draft.Routes.FirstOrDefault(choice => choice.Asset == asset) ?? new AudioRouteChoice { Asset = asset, Bus = asset is MusicTrack ? AudioMixBus.Music : asset is VoiceLine ? AudioMixBus.VO : AudioMixBus.Unchanged }).ToArray();
                routes.itemsSource = draft.Routes; routes.RefreshItems(); status.text = draft.Routes.Length + " routable assets read. Confirm categories; no files changed.";
            }
            catch (Exception error) { Error(error); }
        }
        public void Prepare()
        {
            Invalidate();
            try
            {
                plan = AudioMixPlanner.Prepare(draft);
                foreach (AudioMixChange change in plan.ReadChanges())
                {
                    review.Add(Wrap(change.Path + "\n" + change.Before + " → " + change.After));
                    Foldout why = new Foldout { text = "Why", value = false }; why.Add(Wrap(change.Why)); review.Add(why);
                }
                status.text = plan.AlreadyApplied ? "Already applied and unchanged. No duplicate installation needed." : "Review every change below. Approval creates assets, updates selected routes and saves the target scene.";
                apply.SetEnabled(!plan.AlreadyApplied);
            }
            catch (Exception error) { Error(error); }
        }
        private void Apply()
        {
            try
            {
                AudioMixInstallation receipt = AudioMixPlanner.Apply(plan, draft); plan = null; apply.SetEnabled(false);
                status.messageType = HelpBoxMessageType.Info; status.text = "Applied and saved: " + AssetDatabase.GetAssetPath(receipt) + ". Enter Play Mode to verify and listen."; EditorGUIUtility.PingObject(receipt);
            }
            catch (Exception error) { apply.SetEnabled(false); Error(error); }
        }
        private void Error(Exception error) { status.text = error.Message; status.messageType = HelpBoxMessageType.Error; }
    }
}
