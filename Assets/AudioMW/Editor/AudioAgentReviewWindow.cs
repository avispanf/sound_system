using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioAgentReviewWindow : EditorWindow
    {
        [SerializeField] private string selectedId;
        private string error;
        [MenuItem("Window/AudioMW/Panels/Authoring/Agent Review")]
        public static void Open() { var window = GetWindow<AudioAgentReviewWindow>(); window.titleContent = new GUIContent("Review changes"); window.minSize = new Vector2(360, 320); window.Show(); }
        public static void ShowPlan(string id) { Open(); var window = GetWindow<AudioAgentReviewWindow>(); window.selectedId = id; window.CreateGUI(); }
        private void OnEnable() { AudioAgentBridge.Changed += Refresh; }
        private void OnDisable() { AudioAgentBridge.Changed -= Refresh; }
        private void Refresh() { EditorApplication.delayCall += () => { if (this != null) CreateGUI(); }; }
        private static Label Text(string value) => new Label(value) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 5, marginBottom = 7 } };
        private static string Channels(AudioAgentFile[] files)
        {
            int mono = files.Count(file => file.ForceToMono);
            return "Channel handling · " + (files.Length - mono) + " keep source layout · " + mono + " force mono";
        }
        private static string FileDetails(AudioAgentFile file)
        {
            return file.Source + "\n→ " + file.Destination + "\nChannels: " + (file.ForceToMono ? "Force mono (Unity downmix)" : "Source layout (mono/stereo preserved)") + " · " + (file.Bytes / 1024f).ToString("0.0") + " KB · SHA256 " + file.Sha256.Substring(0, 12);
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            rootVisualElement.AddToClassList("audiomw-inspection");
            rootVisualElement.AddToClassList("audiomw-review");
            rootVisualElement.Add(AudioWorkspace.Navigation("Review"));
            var body = new VisualElement { name = "review-layout" };
            rootVisualElement.Add(body);
            var library = new ScrollView { name = "review-library" };
            body.Add(library);
            var center = new VisualElement { name = "review-center" };
            body.Add(center);
            var scroll = new ScrollView { name = "review-content", viewDataKey = "AudioMW.Review.Scroll" };
            center.Add(scroll);
            var contextHost = new ScrollView { name = "review-context-host" };
            body.Add(contextHost);
            var context = new Foldout { text = "Review context", value = true, name = "review-context" };
            contextHost.Add(context);
            var project = Text(Application.productName); project.AddToClassList("audiomw-eyebrow"); context.Add(project);
            var eyebrow = Text("ASSISTED AUTHORING / REVIEW"); eyebrow.AddToClassList("audiomw-eyebrow"); scroll.Add(eyebrow);
            var title = Text("Review proposed changes"); title.AddToClassList("audiomw-title"); scroll.Add(title);
            var proposals = new Foldout { text = "Proposals · " + AudioAgentBridge.Plans.Count(), value = true, viewDataKey = "AudioMW.Review.Proposals", name = "review-proposals" }; library.Add(proposals);
            body.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool narrow = evt.newRect.width < 900;
                if (body.ClassListContains("audiomw-review-narrow") == narrow && proposals.parent != null && (narrow ? proposals.parent == scroll.contentContainer : proposals.parent == library.contentContainer)) return;
                body.EnableInClassList("audiomw-review-narrow", narrow);
                if (narrow) { scroll.Insert(0, proposals); scroll.Add(context); }
                else { library.Add(proposals); contextHost.Add(context); }
            });
            var connection = new Foldout { text = "Agent connection", value = !AudioAgentBridge.Enabled, viewDataKey = "AudioMW.Review.Connection" }; context.Add(connection);
            connection.Add(Text(AudioAgentBridge.Enabled ? "Connected · This editor session" : "Disconnected"));
            connection.Add(new Button(() => { try { if (AudioAgentBridge.Enabled) AudioAgentBridge.Disable(); else AudioAgentBridge.Enable(); error = null; } catch (Exception e) { error = e.Message; } CreateGUI(); }) { text = AudioAgentBridge.Enabled ? "Disconnect agents" : "Enable local agent bridge", name = "agent-connect" });
            connection.Add(Text(AudioAgentBridge.Enabled ? "Enabled for this editor session. Reconnect after compilation/reload.\n" + System.IO.Path.GetDirectoryName(Application.dataPath) : "Disconnected. No background server or network port is opened."));
            context.Add(new Button(AudioProjectSetupWindow.Open) { text = "Project questionnaire" });
            if (!string.IsNullOrEmpty(error)) scroll.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (var pending in AudioAgentBridge.Plans)
            {
                var data = pending.Read(); var proposal = new Button(() => { selectedId = data.Id; error = null; CreateGUI(); }) { text = pending.State + " · " + (data.Changes.Length + data.Music.Length + (data.HasProfile ? 1 : 0)) + " asset(s) · " + data.Id.Substring(0, 8), tooltip = data.Intent }; proposal.EnableInClassList("audiomw-proposal-selected", data.Id == selectedId); proposals.Add(proposal);
            }
            var plan = AudioAgentBridge.Find(selectedId); if (plan == null) { scroll.Add(Text("Send files through your agent, then choose a proposal here. No assets are imported while preparing a plan.")); return; }
            var review = plan.Read();
            var stateLabel = Text(plan.State + " · " + review.Changes.Length + " events · " + review.Music.Length + " tracks" + (review.HasProfile ? " · state profile" : "")); stateLabel.AddToClassList("audiomw-eyebrow"); scroll.Add(stateLabel);
            scroll.Add(Text(review.Intent));
            context.Add(Text("Scope · Entire plan"));
            context.Add(Text("Captured target · " + review.BuildTarget + "\nUnity " + review.Unity));
            context.Add(Text("To change the scope, reject this plan and request a revised proposal."));
            foreach (var heading in new[] { "Before apply", "After apply", "Conflicts" })
            {
                var label = Text(heading); label.AddToClassList("audiomw-review-heading"); context.Add(label);
                context.Add(Text(heading == "Before apply" ? "Validate asset identity, source files and current plan state." : heading == "After apply" ? "Report the actual result below. Imported audio still needs gameplay wiring and listening checks." : "Stale plans cannot be applied. Request a new proposal after changing the reviewed assets."));
            }
            var destination = new Foldout { text = "Import destination", value = false }; destination.Add(Text(review.Folder)); context.Add(destination);
            foreach (var change in review.Changes)
            {
                var item = new VisualElement(); item.AddToClassList("audiomw-review-item"); scroll.Add(item);
                var heading = Text(change.Name); heading.AddToClassList("audiomw-review-heading"); item.Add(heading);
                var operation = Text(string.IsNullOrEmpty(change.ReplaceEventGuid) ? "Create event · Gameplay hook required" : "Replace source clips · Preserve event identity"); operation.AddToClassList("audiomw-eyebrow"); item.Add(operation);
                item.Add(Text(change.Playback));
                var route = Text("Route: " + change.Routing); route.name = "review-event-route"; item.Add(route);
                var channels = Text(Channels(change.Files)); channels.name = "review-event-channels"; item.Add(channels);
                item.Add(Text("After: " + change.Files.Length + " source clip(s) · " + string.Join(", ", change.Files.Select(file => System.IO.Path.GetFileName(file.Source)))));
                var why = new Foldout { text = "Why / exact change", value = false, viewDataKey = "AudioMW.Review.Why." + review.Id + change.EventPath }; item.Add(why);
                why.Add(Text(change.Purpose));
                var files = new Foldout { text = "File details · " + change.Files.Length, value = false, viewDataKey = "AudioMW.Review.Files." + review.Id + change.EventPath }; why.Add(files);
                files.Add(Text(change.EventPath));
                foreach (var file in change.Files) files.Add(Text(FileDetails(file)));
            }
            foreach (var change in review.Music)
            {
                var settings = change.Settings;
                var item = new VisualElement(); item.AddToClassList("audiomw-review-item"); scroll.Add(item);
                var heading = Text(settings.Name); heading.AddToClassList("audiomw-review-heading"); item.Add(heading);
                var kind = Text("Music track · " + settings.Stems.Length + " stems"); kind.AddToClassList("audiomw-eyebrow"); item.Add(kind);
                item.Add(Text(change.Before));
                item.Add(Text("After: " + settings.Tempo + " BPM · " + settings.BeatsPerBar + " beats/bar · loop on"));
                item.Add(Text("Route: " + change.Route));
                item.Add(Text(Channels(change.Files)));
                var why = new Foldout { text = "Why / exact change", value = false, viewDataKey = "AudioMW.Review.MusicWhy." + review.Id + settings.Name }; item.Add(why);
                why.Add(Text(settings.Purpose + "\n" + change.Target.Path));
                why.Add(Text("Intro: " + (string.IsNullOrEmpty(settings.Intro) ? "none" : settings.Intro) + "\nBody: " + (string.IsNullOrEmpty(settings.Body) ? "stems only" : settings.Body)));
                foreach (var stem in settings.Stems) why.Add(Text("Stem " + stem.Name + " · " + (string.IsNullOrEmpty(stem.ParameterGuid) ? "constant weight " + stem.LowWeight : change.Parameters.First(p => p.Guid == stem.ParameterGuid).Path + " · normalized 0→1 maps to " + stem.LowWeight + "→" + stem.HighWeight) + " · fade " + stem.FadeSeconds + "s"));
                var files = new Foldout { text = "File details · " + change.Files.Length, value = false, viewDataKey = "AudioMW.Review.MusicFiles." + review.Id + settings.Name }; item.Add(files);
                foreach (var file in change.Files) files.Add(Text(FileDetails(file)));
            }
            if (review.HasProfile)
            {
                var p = review.Profile;
                var item = new VisualElement(); item.AddToClassList("audiomw-review-item"); scroll.Add(item);
                var heading = Text("State profile"); heading.AddToClassList("audiomw-review-heading"); item.Add(heading);
                item.Add(Text(p.Target.Path + "\n" + p.Before));
                item.Add(Text("After: " + p.Settings.States.Length + " states · " + p.Settings.Transitions.Length + " transition rules"));
                var rules = new Foldout { text = "Exact states / transitions", value = false }; item.Add(rules);
                foreach (var state in p.Settings.States) rules.Add(Text(state.Name + " → " + state.Track));
                rules.Add(Text("Default transition: " + p.Settings.Quantization + " · " + p.Settings.FadeSeconds + "s · " + p.Settings.Curve));
                foreach (var rule in p.Settings.Transitions) rules.Add(Text(rule.From + " → " + rule.To + " · " + rule.Quantization + " · " + rule.FadeSeconds + "s · " + rule.Curve));
            }
            var limitations = new Foldout { text = "Scope / limitations", value = false }; scroll.Add(limitations);
            foreach (string limit in review.Limitations) limitations.Add(Text(limit));
            if (!string.IsNullOrEmpty(plan.Result))
            {
                if (plan.State == "failed") scroll.Add(new HelpBox(plan.Result, HelpBoxMessageType.Error));
                else { var result = Text(plan.Result); result.AddToClassList("audiomw-eyebrow"); scroll.Add(result); }
            }
            var actions = new VisualElement { name = "review-actions" }; center.Add(actions);
            var buttons = new VisualElement { name = "review-action-buttons" }; actions.Add(buttons);
            var apply = new Button(() =>
            {
                try
                {
                    string problem = AudioAgentPlanner.Check(plan, AudioAgentBridge.Session); if (problem.Length > 0) throw new InvalidOperationException(problem);
                    if (!EditorUtility.DisplayDialog("Apply AudioMW integration?", review.Changes.Length + " event changes, " + review.Music.Length + " music tracks" + (review.HasProfile ? " and a state profile" : "") + "; imported audio in " + review.Folder + ".\nReview " + plan.Digest.Substring(0, 12) + ".\nExisting asset content will be replaced exactly as shown. No gameplay code or mixer gains will be changed.", "Apply reviewed plan", "Cancel")) return;
                    AudioAgentBridge.Approve(plan); error = null;
                }
                catch (Exception e) { error = e.Message; }
                CreateGUI();
            }) { text = "Approve and apply in Unity", name = "agent-approve" }; apply.SetEnabled(plan.State == "pending" && AudioAgentBridge.Enabled); apply.AddToClassList("audiomw-review-apply"); buttons.Add(apply);
            var reject = new Button(() => { AudioAgentBridge.Reject(plan); CreateGUI(); }) { text = "Reject plan", name = "agent-reject" }; reject.SetEnabled(plan.State == "pending"); buttons.Add(reject);
            var confirmation = Text("Only explicit confirmation in Unity applies the complete plan."); confirmation.AddToClassList("audiomw-review-confirmation"); actions.Add(confirmation);
        }
    }
}
