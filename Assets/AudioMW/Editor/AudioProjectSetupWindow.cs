using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioProjectSetupWindow : EditorWindow
    {
        [SerializeField] private AudioProjectBrief draft = new AudioProjectBrief();
        private AudioProjectPlan plan;
        private VisualElement review;
        private HelpBox status;
        private Button save;

        [MenuItem("Window/AudioMW/Project Setup")]
        public static void Open()
        {
            AudioProjectSetupWindow window = GetWindow<AudioProjectSetupWindow>();
            window.titleContent = new GUIContent("Project Setup"); window.minSize = new Vector2(380, 420); window.Show();
        }

        public void LoadDraft(AudioProjectBrief value)
        {
            draft = JsonUtility.FromJson<AudioProjectBrief>(JsonUtility.ToJson(value)); plan = null; CreateGUI();
        }

        public void CreateGUI()
        {
            if (draft == null) draft = new AudioProjectBrief();
            if (string.IsNullOrEmpty(draft.Game)) draft.Game = Application.productName;
            if (string.IsNullOrEmpty(draft.Platforms)) draft.Platforms = EditorUserBuildSettings.activeBuildTarget.ToString();
            plan = null;
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            ScrollView scroll = new ScrollView(); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            Label title = new Label("Project Setup"); title.style.unityFontStyleAndWeight = FontStyle.Bold; scroll.Add(title);
            scroll.Add(new Button(AudioMixSetupWindow.Open) { text = "Configure routing and ducking", tooltip = "Prepare a separate executable mix plan using a saved setup profile." });
            scroll.Add(Wrap("Describe the game, inspect existing audio, then approve a saved setup plan. Routing, importers and gains stay unchanged in this first stage."));
            ObjectField existing = new ObjectField("Load setup") { objectType = typeof(AudioProjectSetup), allowSceneObjects = false };
            existing.RegisterValueChangedCallback(evt =>
            {
                if (!(evt.newValue is AudioProjectSetup setup)) return;
                AudioProjectBrief value = setup.Brief; value.ProfilePath = AssetDatabase.GetAssetPath(setup); LoadDraft(value);
            }); scroll.Add(existing);
            Text(scroll, "Game", "setup-game", draft.Game, value => draft.Game = value);
            Text(scroll, "Genre / core actions", "setup-genre", draft.Genre, value => draft.Genre = value);
            Text(scroll, "Target audience", "setup-audience", draft.Audience, value => draft.Audience = value);
            Text(scroll, "Target platforms", "setup-platforms", draft.Platforms, value => draft.Platforms = value);
            ObjectField source = new ObjectField("Content folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(draft.SourceFolder), name = "setup-source" };
            source.RegisterValueChangedCallback(evt => { draft.SourceFolder = AssetDatabase.GetAssetPath(evt.newValue); Invalidate(); }); scroll.Add(source);
            Foldout advanced = new Foldout { text = "Mix, devices and budgets", value = false }; scroll.Add(advanced);
            Text(advanced, "Camera / perspective", "setup-perspective", draft.Perspective, value => draft.Perspective = value);
            Text(advanced, "Listening devices", "setup-devices", draft.ListeningDevices, value => draft.ListeningDevices = value);
            Text(advanced, "Mix / dialogue intent", "setup-mix", draft.MixIntent, value => draft.MixIntent = value);
            Text(advanced, "Delivery target", "setup-target", draft.DeliveryTarget, value => draft.DeliveryTarget = value);
            advanced.Add(Wrap("Optional: a chosen delivery requirement or reference. No universal platform LUFS target is assumed."));
            Text(advanced, "Content / rights notes", "setup-content", draft.ContentNotes, value => draft.ContentNotes = value);
            IntegerField voices = new IntegerField("Voice budget") { value = draft.VoiceBudget }; voices.RegisterValueChangedCallback(evt => { draft.VoiceBudget = evt.newValue; Invalidate(); }); advanced.Add(voices);
            IntegerField memory = new IntegerField("Audio memory budget MB") { value = draft.MemoryBudgetMb }; memory.RegisterValueChangedCallback(evt => { draft.MemoryBudgetMb = evt.newValue; Invalidate(); }); advanced.Add(memory);
            advanced.Add(Wrap("Budgets express intent. This metadata analysis does not measure memory, CPU, rendered loudness or frequency balance."));
            Text(scroll, "Setup asset path", "setup-path", draft.ProfilePath, value => draft.ProfilePath = value);
            Button analyse = new Button(Analyse) { text = "Analyse and prepare review", name = "setup-analyse" }; scroll.Add(analyse);
            status = new HelpBox("Draft only. Analyse to review before saving. Required: game, genre, audience, platforms and an existing content folder.", HelpBoxMessageType.Info); scroll.Add(status);
            review = new VisualElement { name = "setup-review" }; scroll.Add(review);
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            save = new Button(Save) { text = "Approve and save plan", name = "setup-approve", tooltip = "Writes only the setup profile and reviewed recommendations. All inputs are checked again before saving." }; save.SetEnabled(false); actions.Add(save);
            actions.Add(new Button(Reject) { text = "Reject plan", name = "setup-reject" }); scroll.Add(actions);
        }

        private static Label Wrap(string value)
        {
            return new Label(value) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 4, marginBottom = 6 } };
        }
        private void Text(VisualElement parent, string label, string name, string value, Action<string> setter)
        {
            TextField field = new TextField(label) { name = name, value = value };
            field.RegisterValueChangedCallback(evt => { setter(evt.newValue); Invalidate(); }); parent.Add(field);
        }
        private void Invalidate()
        {
            plan = null; review?.Clear(); save?.SetEnabled(false);
            if (status != null) { status.text = "Brief changed. Analyse again to review the exact new plan."; status.messageType = HelpBoxMessageType.Info; }
        }
        public void Reject()
        {
            Invalidate(); status.text = "Plan rejected. No assets or settings changed. Your draft is retained.";
        }
        public void Analyse()
        {
            Invalidate();
            try
            {
                plan = AudioProjectPlanner.Analyse(draft);
                AudioProjectReviewData data = plan.Read();
                status.text = data.Inventory.Count + " audio items · " + data.AuditErrors + " library errors · " + data.AuditWarnings + " warnings. Review below. No changes applied.";
                status.messageType = data.AuditErrors > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
                review.Add(Wrap((AssetDatabase.LoadAssetAtPath<AudioProjectSetup>(draft.ProfilePath) == null ? "Create " : "Update ") + draft.ProfilePath + " with this brief and reviewed plan. This is the only executable change in this batch."));
                Foldout inventory = new Foldout { text = "Existing audio inventory (read-only)", value = false }; review.Add(inventory);
                inventory.Add(Wrap("Selected content folder plus existing mixers and banks under Assets. No new files are imported. File roles and gameplay hooks are unconfirmed."));
                ListView items = new ListView { itemsSource = data.Inventory, fixedItemHeight = 24, selectionType = SelectionType.Single };
                items.style.height = 220; items.makeItem = () => new Label();
                items.bindItem = (element, index) =>
                {
                    AudioProjectInventoryItem item = data.Inventory[index]; Label label = (Label)element;
                    label.text = item.Kind + " · " + item.Path; label.tooltip = item.Detail;
                };
                Label selected = Wrap("Select an item for metadata.");
                items.selectionChanged += selection =>
                {
                    AudioProjectInventoryItem item = selection.OfType<AudioProjectInventoryItem>().FirstOrDefault();
                    selected.text = item == null ? "Select an item for metadata." : item.Path + "\n" + item.Detail;
                };
                inventory.Add(items); inventory.Add(selected);
                if (data.Issues.Count > 0)
                {
                    Foldout issues = new Foldout { text = "Captured library diagnostics", value = false }; review.Add(issues);
                    foreach (AudioLibraryIssue issue in data.Issues) issues.Add(Wrap(issue.Severity + " · " + issue.AssetPath + "\n" + issue.Message));
                }
                foreach (AudioProjectRecommendation item in data.Recommendations)
                {
                    Foldout why = new Foldout { text = "Why: " + item.Title, value = false }; why.Add(Wrap(item.Reason)); review.Add(why);
                }
                review.Add(Wrap("Saving records these recommendations for later review. It does not authorize future routing, import, gain or code edits."));
                save.SetEnabled(true);
            }
            catch (Exception error) { status.text = error.Message; status.messageType = HelpBoxMessageType.Error; }
        }
        private void Save()
        {
            try
            {
                AudioProjectSetup asset = AudioProjectPlanner.SaveReviewedPlan(plan, draft);
                plan = null; save.SetEnabled(false); status.messageType = HelpBoxMessageType.Info;
                status.text = "Saved setup revision " + asset.Revision + ": " + AssetDatabase.GetAssetPath(asset) + ". Routing and gains unchanged.";
                EditorGUIUtility.PingObject(asset);
            }
            catch (Exception error) { status.text = error.Message; status.messageType = HelpBoxMessageType.Error; save.SetEnabled(false); }
        }
    }

    [CustomEditor(typeof(AudioProjectSetup))]
    public sealed class AudioProjectSetupInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AudioProjectSetup asset = (AudioProjectSetup)target;
            AudioProjectBrief brief = asset.Brief;
            EditorGUILayout.LabelField("Saved setup plan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Revision", asset.Revision.ToString());
            EditorGUILayout.LabelField("Game", brief.Game);
            EditorGUILayout.LabelField("Platforms", brief.Platforms);
            EditorGUILayout.HelpBox("Reviewed recommendations only. No mixer, importer, gain or gameplay edits have been applied by this profile.", MessageType.Info);
            if (GUILayout.Button("Open Project Setup"))
            {
                AudioProjectSetupWindow.Open(); brief.ProfilePath = AssetDatabase.GetAssetPath(asset);
                EditorWindow.GetWindow<AudioProjectSetupWindow>().LoadDraft(brief);
            }
        }
    }
}
