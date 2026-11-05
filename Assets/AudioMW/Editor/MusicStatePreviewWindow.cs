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

        [MenuItem("Window/AudioMW/Music Transitions")]
        public static void OpenMenu() { Open(null); }

        public static void Open(MusicStateProfile selected)
        {
            MusicStatePreviewWindow window = GetWindow<MusicStatePreviewWindow>();
            window.titleContent = new GUIContent("Music Transitions");
            window.minSize = new Vector2(360, 260);
            if (selected != null) window.profile = selected;
            window.CreateGUI();
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            ObjectField field = new ObjectField("State profile") { objectType = typeof(MusicStateProfile), value = profile, allowSceneObjects = false };
            field.RegisterValueChangedCallback(evt => { profile = evt.newValue as MusicStateProfile; Rebuild(); });
            root.Add(field);
            help = new HelpBox("", HelpBoxMessageType.Info);
            root.Add(help);
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = status.style.marginBottom = 12;
            root.Add(status);
            stateButtons = new ScrollView();
            stateButtons.style.flexGrow = 1;
            root.Add(stateButtons);
            Button stop = new Button(AudioSystem.StopMusic) { text = "Stop music" };
            root.Add(stop);
            root.schedule.Execute(() => { stop.SetEnabled(EditorApplication.isPlaying && AudioRuntime.Exists); UpdateStatus(); }).Every(100);
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
                    stateButtons.Add(new Button(() => { if (EditorApplication.isPlaying) profile.Request(AudioSystem.Music, name); }) { text = name });
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
            if (!EditorApplication.isPlaying || !AudioRuntime.Exists) { status.text = "Music stopped"; return; }
            MusicPlayer player = AudioSystem.Music;
            string current = player.CurrentTrack != null ? player.CurrentTrack.name : "Stopped";
            status.text = "Playing: " + current;
            if (player.IsTransitionPending)
                status.text += "\nNext: " + player.PendingTrack.name + " • in " + System.Math.Max(0, player.PendingStartDspTime - AudioSettings.dspTime).ToString("F2") + " s";
        }
    }
}
