using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class MusicStatePreviewWindow : EditorWindow
    {
        [SerializeField] private MusicStateProfile profile;
        private VisualElement stateButtons;
        private Label status;
        private HelpBox help;
        private IVisualElementScheduledItem refresh;

        [MenuItem("Window/AudioMW/Music Transitions")]
        public static void OpenMenu() { Open(null); }

        public static void Open(MusicStateProfile selected)
        {
            MusicStatePreviewWindow window = GetWindow<MusicStatePreviewWindow>();
            window.titleContent = new GUIContent("Music");
            window.minSize = new Vector2(360, 260);
            if (selected != null) window.profile = selected;
            window.CreateGUI();
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            if (refresh != null) refresh.Pause();
            root.Clear();
            AudioWorkspace.Style(root);
            root.Add(AudioWorkspace.Navigation("Music"));
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            ObjectField field = new ObjectField("State profile") { objectType = typeof(MusicStateProfile), value = profile, allowSceneObjects = false };
            field.RegisterValueChangedCallback(evt => { profile = evt.newValue as MusicStateProfile; Rebuild(); });
            root.Add(field);
            Button settings = new Button(() => { if (profile != null) Selection.activeObject = profile; }) { text = "Edit profile in Inspector" };
            root.Add(settings);
            help = new HelpBox("", HelpBoxMessageType.Info);
            root.Add(help);
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = status.style.marginBottom = 12;
            root.Add(status);
            root.Add(new Label("Request state · Profile rules") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            stateButtons = new ScrollView();
            stateButtons.style.flexGrow = 1;
            root.Add(stateButtons);
            Button stop = new Button(AudioSystem.StopMusic) { text = "Stop music" };
            Button cancel = new Button(() => { if (EditorApplication.isPlaying && AudioRuntime.Exists) AudioSystem.Music.CancelTransition(); }) { text = "Cancel pending transition", tooltip = "Cancel a transition before its scheduled boundary. A crossfade that has already started continues." };
            root.Add(cancel);
            root.Add(stop);
            refresh = root.schedule.Execute(() => { stop.SetEnabled(EditorApplication.isPlaying && AudioRuntime.Exists); cancel.SetEnabled(EditorApplication.isPlaying && AudioRuntime.Exists && AudioSystem.Music.IsTransitionPending && AudioSettings.dspTime < AudioSystem.Music.PendingStartDspTime); settings.SetEnabled(profile != null); UpdateStatus(); }).Every(100);
            Rebuild();
        }

        private void Rebuild()
        {
            if (stateButtons == null) return;
            stateButtons.Clear();
            if (profile != null)
                foreach (MusicState state in profile.States)
                {
                    if (state == null) continue;
                    string name = state.Name;
                    Button request = new Button(() => { if (EditorApplication.isPlaying && profile != null) profile.Request(AudioSystem.Music, name); }) { text = name, name = "request-state", userData = state };
                    request.style.whiteSpace = WhiteSpace.Normal;
                    stateButtons.Add(request);
                }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (help == null) return;
            string error = profile == null ? "Choose a music state profile." : profile.Validate();
            bool ready = string.IsNullOrEmpty(error) && EditorApplication.isPlaying;
            stateButtons.SetEnabled(ready);
            help.text = !string.IsNullOrEmpty(error) ? error : !EditorApplication.isPlaying ? "Enter Play Mode to audition transitions through the scene audio runtime." : "Select a state to hear its transition. This controls the running scene's music.";
            if (!EditorApplication.isPlaying) { status.text = "Current: Unavailable outside Play Mode\nPending: Unavailable outside Play Mode"; UpdateRules(null); return; }
            if (!AudioRuntime.Exists) { status.text = "Current: Runtime not started\nPending: None"; UpdateRules(null); return; }
            MusicPlayer player = AudioSystem.Music;
            string current = player.CurrentTrack != null ? player.CurrentTrack.name : "Stopped";
            status.text = "Current: " + current + "\nPending: " + (player.IsTransitionPending ? player.PendingTrack.name : "None");
            UpdateRules(player.CurrentTrack);
            if (player.IsCrossfading)
                status.text += "\nCrossfading • gain " + player.TransitionGain.ToString("F2") + " • " + System.Math.Max(0, player.TransitionEndDspTime - AudioSettings.dspTime).ToString("F2") + " s remaining";
            if (!player.SupportsDspFades) status.text += "\nDSP crossfades are unavailable on this platform; transitions use cuts.";
            if (player.IsTransitionPending)
                status.text += "\nBoundary: " + player.PendingQuantization + " · in " + System.Math.Max(0, player.PendingStartDspTime - AudioSettings.dspTime).ToString("F2") + " s\nScheduled crossfade: " + player.PendingFadeSeconds.ToString("0.##") + " s · " + player.PendingCurve;
        }
        private void UpdateRules(MusicTrack current)
        {
            if (profile == null) return;
            string from = profile.StateFor(current);
            stateButtons.Query<Button>("request-state").ForEach(button =>
            {
                MusicState state = button.userData as MusicState;
                if (state == null) return;
                MusicQuantization boundary = profile.DefaultQuantization;
                float seconds = profile.DefaultFadeSeconds;
                MusicFadeCurve curve = profile.DefaultCurve;
                foreach (MusicTransitionRule rule in profile.Transitions)
                    if (rule != null && rule.From == from && rule.To == state.Name)
                    { boundary = rule.Quantization; seconds = rule.FadeSeconds; curve = rule.Curve; break; }
                string timing = current == null ? "Start from silence" : boundary + " · " + seconds.ToString("0.##") + " s · " + curve;
                button.text = state.Name + " · " + timing;
                button.tooltip = state.Track == null ? "Missing track" : state.Track.name + "\n" + timing;
            });
        }
    }
}
