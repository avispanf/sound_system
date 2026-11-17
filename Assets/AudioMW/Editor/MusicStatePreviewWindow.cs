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
        private MusicState[] displayedStates = new MusicState[0];
        private Label status;
        private Label timing;
        private Label ruleDetails;
        private HelpBox help;
        private ScrollView library;
        private VisualElement profileList;
        [SerializeField] private string profileQuery = "";
        private ObjectField profileField;
        private Label profileTitle;
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

        private void OnProjectChange() { RebuildLibrary(); }

        private void RebuildLibrary()
        {
            if (profileList == null) return;
            profileList.Clear();
            int matches = 0;
            string[] ids = AssetDatabase.FindAssets("t:MusicStateProfile");
            System.Array.Sort(ids, (a, b) => string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), System.StringComparison.OrdinalIgnoreCase));
            foreach (string id in ids)
            {
                var item = AssetDatabase.LoadAssetAtPath<MusicStateProfile>(AssetDatabase.GUIDToAssetPath(id));
                if (item == null || item.name.IndexOf(profileQuery, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                matches++;
                var button = new Button(() => { profileField.value = item; Selection.activeObject = item; }) { text = item.name, tooltip = AssetDatabase.GetAssetPath(item) };
                button.EnableInClassList("audiomw-primary", profile == item); profileList.Add(button);
            }
            if (matches == 0) profileList.Add(new Label(ids.Length == 0 ? "No music state profiles in this project." : "No matching profiles. Clear the search to see all profiles.") { style = { whiteSpace = WhiteSpace.Normal } });
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            if (refresh != null) refresh.Pause();
            root.Clear();
            AudioWorkspace.Style(root);
            root.Add(AudioWorkspace.Navigation("Music"));
            root.AddToClassList("audiomw-inspection");
            root.AddToClassList("audiomw-music");
            var layout = new VisualElement { name = "music-layout" }; root.Add(layout);
            library = new ScrollView { name = "music-library" }; layout.Add(library);
            var heading = new Label("Music profiles"); heading.AddToClassList("audiomw-panel-heading"); library.Add(heading);
            var search = new ToolbarSearchField { name = "music-profile-search", tooltip = "Find a music state profile by name." };
            search.style.minWidth = 0; search.style.width = Length.Percent(100); search.style.marginLeft = search.style.marginRight = 0;
            search.SetValueWithoutNotify(profileQuery); search.RegisterValueChangedCallback(evt => { profileQuery = evt.newValue; RebuildLibrary(); }); library.Add(search);
            profileList = new VisualElement(); library.Add(profileList);
            ScrollView content = new ScrollView { name = "music-content" };
            content.style.flexGrow = 1;
            layout.Add(content);
            var contextHost = new ScrollView { name = "music-context-host" }; layout.Add(contextHost);
            var context = new VisualElement { name = "music-context" }; contextHost.Add(context);
            var contextTitle = new Label("Transition settings"); contextTitle.AddToClassList("audiomw-panel-heading"); context.Add(contextTitle);
            layout.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool narrow = evt.newRect.width < 900;
                if (layout.ClassListContains("audiomw-music-narrow") == narrow && (narrow ? context.parent == content.contentContainer : context.parent == contextHost.contentContainer)) return;
                layout.EnableInClassList("audiomw-music-narrow", narrow);
                if (narrow) content.Add(context); else contextHost.Add(context);
            });
            root = content;
            Label eyebrow = new Label("ADAPTIVE MUSIC / SCENE RUNTIME");
            eyebrow.AddToClassList("audiomw-eyebrow"); root.Add(eyebrow);
            Label title = new Label("Music transitions");
            title.AddToClassList("audiomw-title"); root.Add(title); profileTitle = title;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            ObjectField field = new ObjectField("State profile") { objectType = typeof(MusicStateProfile), value = profile, allowSceneObjects = false };
            profileField = field;
            field.RegisterValueChangedCallback(evt => { profile = evt.newValue as MusicStateProfile; Rebuild(); RebuildLibrary(); RefreshView(); });
            root.Add(field);
            Button settings = new Button(() => { if (profile != null) Selection.activeObject = profile; }) { text = "Edit profile in Inspector" };
            context.Add(settings);
            help = new HelpBox("", HelpBoxMessageType.Info);
            context.Add(help);
            ruleDetails = new Label { name = "music-rule-details" };
            ruleDetails.style.whiteSpace = WhiteSpace.Normal;
            ruleDetails.AddToClassList("audiomw-runtime-card");
            context.Add(ruleDetails);
            status = new Label { name = "music-current-pending" };
            status.AddToClassList("audiomw-runtime-card");
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = status.style.marginBottom = 12;
            root.Add(status);
            timing = new Label { name = "music-timing" };
            timing.AddToClassList("audiomw-runtime-card");
            root.Add(timing);
            var stateHeading = new Label("Request state") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            root.Add(stateHeading);
            stateButtons = new VisualElement { name = "music-states" };
            stateButtons.style.flexDirection = FlexDirection.Row;
            stateButtons.style.flexWrap = Wrap.Wrap;
            root.Add(stateButtons);
            root.Insert(root.IndexOf(status), stateHeading);
            root.Insert(root.IndexOf(status), stateButtons);
            Button stop = new Button(() => { if (EditorApplication.isPlaying && AudioRuntime.Exists) AudioSystem.StopMusic(); }) { text = "Stop music", name = "stop-scene-music" };
            stop.SetEnabled(false);
            Button cancel = new Button(() => { if (EditorApplication.isPlaying && AudioRuntime.Exists) AudioSystem.Music.CancelTransition(); }) { text = "Cancel pending transition", name = "cancel-pending-music", tooltip = "Cancel a transition before its scheduled boundary. A crossfade that has already started continues." };
            cancel.SetEnabled(false);
            context.Add(cancel);
            context.Add(stop);
            refresh = root.schedule.Execute(() => { stop.SetEnabled(EditorApplication.isPlaying && AudioRuntime.Exists); cancel.SetEnabled(EditorApplication.isPlaying && AudioRuntime.Exists && AudioSystem.Music.IsTransitionPending && AudioSettings.dspTime < AudioSystem.Music.PendingStartDspTime); settings.SetEnabled(profile != null); RefreshView(); }).Every(100);
            RebuildLibrary(); Rebuild(); RefreshView();
        }

        private void Rebuild()
        {
            if (stateButtons == null) return;
            stateButtons.Clear();
            displayedStates = profile != null ? (MusicState[])profile.States.Clone() : new MusicState[0];
            if (profile != null)
                foreach (MusicState state in profile.States)
                {
                    if (state == null) continue;
                    Button request = new Button(() => { if (EditorApplication.isPlaying && profile != null) profile.Request(AudioSystem.Music, state.Name); }) { text = state.Name, name = "request-state", userData = state };
                    request.style.whiteSpace = WhiteSpace.Normal;
                    stateButtons.Add(request);
                }
        }

        public void RefreshView()
        {
            MusicState[] states = profile != null ? profile.States : new MusicState[0];
            bool changed = states.Length != displayedStates.Length;
            if (!changed) for (int i = 0; i < states.Length; i++) if (!ReferenceEquals(states[i], displayedStates[i])) { changed = true; break; }
            if (changed) Rebuild();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (help == null) return;
            profileTitle.text = profile != null ? profile.name : "Music transitions";
            string error = profile == null ? "Choose a music state profile." : profile.Validate();
            bool ready = string.IsNullOrEmpty(error) && EditorApplication.isPlaying;
            timing.text = "Timing · Unavailable outside Play Mode";
            stateButtons.SetEnabled(ready);
            help.text = !string.IsNullOrEmpty(error) ? error : !EditorApplication.isPlaying ? "Enter Play Mode to audition transitions through the scene audio runtime." : "Select a state to hear its transition. This controls the running scene's music.";
            if (!EditorApplication.isPlaying) { status.text = "Current: Unavailable outside Play Mode\nPending: Unavailable outside Play Mode"; UpdateRules(null); return; }
            if (!AudioRuntime.Exists) { status.text = "Current: Unavailable · runtime not started\nPending: Unavailable · runtime not started"; timing.text = "Timing · Unavailable until the scene runtime starts"; UpdateRules(null); return; }
            MusicPlayer player = AudioSystem.Music;
            string current = player.CurrentTrack != null ? player.CurrentTrack.name : "Stopped";
            status.text = "Current: " + current + "\nPending: " + (player.IsTransitionPending ? player.PendingTrack.name : "None");
            UpdateRules(player.CurrentTrack);
            timing.text = "Timing · No scheduled transition";
            if (player.IsCrossfading)
                timing.text = "Crossfading • gain " + player.TransitionGain.ToString("F2") + " • " + System.Math.Max(0, player.TransitionEndDspTime - AudioSettings.dspTime).ToString("F2") + " s remaining";
            if (player.IsTransitionPending)
                timing.text = "Scheduled timing · Captured\nBoundary: " + player.PendingQuantization + " · in " + System.Math.Max(0, player.PendingStartDspTime - AudioSettings.dspTime).ToString("F2") + " s\nScheduled crossfade: " + player.PendingFadeSeconds.ToString("0.##") + " s · " + player.PendingCurve + "\nProfile edits do not change this queued transition.";
            if (!player.SupportsDspFades) timing.text += "\nDSP crossfades are unavailable on this platform; transitions use cuts.";
        }
        private void UpdateRules(MusicTrack current)
        {
            if (profile == null) { ruleDetails.text = "Rules unavailable: choose a music state profile."; return; }
            string from = profile.StateFor(current);
            var rules = new System.Text.StringBuilder();
            rules.Append("Profile rules · ").Append(current == null ? "start from silence" : string.IsNullOrEmpty(from) ? "current track outside profile" : "from " + from);
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
                button.text = state.Name;
                rules.Append("\n").Append(state.Name).Append(" → ").Append(timing);
                button.tooltip = state.Track == null ? "Missing track" : state.Track.name + "\n" + timing;
            });
            ruleDetails.text = rules.ToString();
        }
    }
}
