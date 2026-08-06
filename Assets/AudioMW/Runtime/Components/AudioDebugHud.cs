using System.Text;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Audio Debug HUD")]
    public sealed class AudioDebugHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
#if ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
#endif
        [SerializeField] private int fontSize = 12;
        [SerializeField] private Vector2 origin = new Vector2(10f, 10f);
        [SerializeField] private bool listVoices = true;
        [SerializeField] private int maxListedVoices = 12;

        private readonly StringBuilder builder = new StringBuilder(512);
        private GUIStyle style;

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public string BuildReport()
        {
            builder.Length = 0;

            if (!AudioRuntime.Exists)
            {
                builder.Append("AudioMW: runtime not started");
                return builder.ToString();
            }

            AudioRuntime runtime = AudioRuntime.Instance;
            VoicePool pool = runtime.Pool;

            builder.Append("AudioMW\n");
            builder.Append("voices ").Append(pool.ActiveCount).Append(" / ").Append(pool.TotalCount)
                   .Append(" (max ").Append(pool.MaxVoices).Append(")\n");
            builder.Append("plays ").Append(runtime.PlayRequests)
                   .Append("  rejected ").Append(runtime.RejectedRequests)
                   .Append("  steals ").Append(pool.StealCount).Append('\n');
            builder.Append("params ").Append(runtime.GlobalParameters.Count).Append('\n');

            if (!listVoices)
            {
                return builder.ToString();
            }

            int listed = 0;
            System.Collections.Generic.IReadOnlyList<Voice> voices = pool.Voices;

            for (int i = 0; i < voices.Count && listed < maxListedVoices; i++)
            {
                Voice voice = voices[i];
                if (!voice.IsActive)
                {
                    continue;
                }

                listed++;
                builder.Append("  ");
                builder.Append(voice.CurrentEvent != null ? voice.CurrentEvent.name : "(none)");
                builder.Append("  vol ").Append(voice.Source.volume.ToString("F2"));
                builder.Append("  pitch ").Append(voice.Source.pitch.ToString("F2"));

                if (voice.BlendLayer != null)
                {
                    builder.Append("  [blend]");
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }

        public void ToggleVisibility()
        {
            visible = !visible;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                ToggleVisibility();
            }
        }
#endif

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.UpperLeft;
                style.normal.textColor = Color.white;
            }

            style.fontSize = fontSize;

            string report = BuildReport();
            Vector2 size = style.CalcSize(new GUIContent(report));
            Rect rect = new Rect(origin.x, origin.y, size.x + 16f, size.y + 12f);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, rect.height), report, style);
        }
    }
}
