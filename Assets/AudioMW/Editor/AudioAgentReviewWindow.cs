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
        [MenuItem("Window/AudioMW/Agent Review")]
        public static void Open() { var window = GetWindow<AudioAgentReviewWindow>(); window.titleContent = new GUIContent("Agent Review"); window.minSize = new Vector2(440, 420); window.Show(); }
        public static void ShowPlan(string id) { Open(); var window = GetWindow<AudioAgentReviewWindow>(); window.selectedId = id; window.CreateGUI(); }
        private void OnEnable() { AudioAgentBridge.Changed += Refresh; }
        private void OnDisable() { AudioAgentBridge.Changed -= Refresh; }
        private void Refresh() { EditorApplication.delayCall += () => { if (this != null) CreateGUI(); }; }
        private static Label Text(string value) => new Label(value) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 5, marginBottom = 7 } };
        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.paddingLeft = 12; scroll.style.paddingRight = 12; rootVisualElement.Add(scroll);
            scroll.Add(Text("Agent Review · " + Application.productName));
            scroll.Add(Text("Local audio files → events or adaptive music → your review. MCP cannot approve or apply changes. Existing event references are preserved."));
            scroll.Add(new Button(() => { try { if (AudioAgentBridge.Enabled) AudioAgentBridge.Disable(); else AudioAgentBridge.Enable(); error = null; } catch (Exception e) { error = e.Message; } CreateGUI(); }) { text = AudioAgentBridge.Enabled ? "Disconnect agents" : "Enable local agent bridge", name = "agent-connect" });
            scroll.Add(Text(AudioAgentBridge.Enabled ? "Enabled for this editor session. Reconnect after compilation/reload.\n" + System.IO.Path.GetDirectoryName(Application.dataPath) : "Disconnected. No background server or network port is opened."));
            scroll.Add(new Button(AudioProjectSetupWindow.Open) { text = "Project questionnaire" });
            if (!string.IsNullOrEmpty(error)) scroll.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (var pending in AudioAgentBridge.Plans)
            {
                var data = pending.Read(); scroll.Add(new Button(() => { selectedId = data.Id; error = null; CreateGUI(); }) { text = pending.State + " · " + (data.Changes.Length + data.Music.Length) + " asset(s) · " + data.Id.Substring(0, 8), tooltip = data.Intent });
            }
            var plan = AudioAgentBridge.Find(selectedId); if (plan == null) { scroll.Add(Text("Send files through your agent, then choose a proposal here. No assets are imported while preparing a plan.")); return; }
            var review = plan.Read(); scroll.Add(Text("Review " + review.Id.Substring(0, 8) + " · " + plan.State)); scroll.Add(Text(review.Intent));
            scroll.Add(Text("Create import folder: " + review.Folder));
            foreach (var change in review.Changes)
            {
                var item = new Foldout { text = (string.IsNullOrEmpty(change.ReplaceEventGuid) ? "Create event: " : "Replace clips: ") + change.Name, value = true }; scroll.Add(item);
                item.Add(Text(change.EventPath + "\nWhy: " + change.Purpose + "\nRoute: " + change.Routing));
                item.Add(Text(change.Playback));
                foreach (var file in change.Files) item.Add(Text(file.Source + "\n→ " + file.Destination + "\n" + (file.Bytes / 1024f).ToString("0.0") + " KB · SHA256 " + file.Sha256.Substring(0, 12)));
            }
            foreach (var change in review.Music)
            {
                var settings = change.Settings;
                var item = new Foldout { text = "Music: " + settings.Name, value = true }; scroll.Add(item);
                item.Add(Text(change.Target.Path + "\nWhy: " + settings.Purpose + "\n" + change.Before));
                item.Add(Text("After: " + settings.Tempo + " BPM · " + settings.BeatsPerBar + " beats/bar · loop on\nRoute: " + change.Route));
                item.Add(Text("Intro: " + (string.IsNullOrEmpty(settings.Intro) ? "none" : settings.Intro) + "\nBody: " + (string.IsNullOrEmpty(settings.Body) ? "stems only" : settings.Body)));
                foreach (var stem in settings.Stems) item.Add(Text("Stem " + stem.Name + " · " + (string.IsNullOrEmpty(stem.ParameterGuid) ? "constant weight " + stem.LowWeight : change.Parameters.First(p => p.Guid == stem.ParameterGuid).Path + " · normalized 0→1 maps to " + stem.LowWeight + "→" + stem.HighWeight) + " · fade " + stem.FadeSeconds + "s"));
                foreach (var file in change.Files) item.Add(Text(file.Source + "\n→ " + file.Destination + "\nChannels: " + (file.ForceToMono ? "Force mono (Unity downmix)" : "Source layout (mono/stereo preserved)") + " · " + (file.Bytes / 1024f).ToString("0.0") + " KB · SHA256 " + file.Sha256.Substring(0, 12)));
            }
            if (review.HasProfile)
            {
                var p = review.Profile; scroll.Add(Text("State profile: " + p.Target.Path + "\n" + p.Before));
                foreach (var state in p.Settings.States) scroll.Add(Text(state.Name + " → " + state.Track));
                scroll.Add(Text("Default transition: " + p.Settings.Quantization + " · " + p.Settings.FadeSeconds + "s · " + p.Settings.Curve));
                foreach (var rule in p.Settings.Transitions) scroll.Add(Text(rule.From + " → " + rule.To + " · " + rule.Quantization + " · " + rule.FadeSeconds + "s · " + rule.Curve));
            }
            foreach (string limit in review.Limitations) scroll.Add(new HelpBox(limit, HelpBoxMessageType.Info));
            scroll.Add(Text(plan.Result));
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
            }) { text = "Approve and apply in Unity", name = "agent-approve" }; apply.SetEnabled(plan.State == "pending" && AudioAgentBridge.Enabled); scroll.Add(apply);
            var reject = new Button(() => { AudioAgentBridge.Reject(plan); CreateGUI(); }) { text = "Reject plan", name = "agent-reject" }; reject.SetEnabled(plan.State == "pending"); scroll.Add(reject);
        }
    }
}
