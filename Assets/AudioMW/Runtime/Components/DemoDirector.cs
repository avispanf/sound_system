using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Demo Director")]
    public sealed class DemoDirector : MonoBehaviour
    {
        [SerializeField] private MusicTrack track;
        [SerializeField] private SoundParameter intensity;
        [SerializeField] private SoundParameter voiceDuck;
        [SerializeField] private AudioClip stinger;
        [SerializeField] private SoundEvent blips;
        [SerializeField] private SoundEvent impact;
        [SerializeField] private VoiceLine firstLine;
        [SerializeField] private VoiceLine secondLine;

        [SerializeField] private float intensityStep = 0.25f;

        private string subtitle = string.Empty;
        private float currentIntensity;

        public float CurrentIntensity
        {
            get { return currentIntensity; }
        }

        public string Subtitle
        {
            get { return subtitle; }
        }

        private void Start()
        {
            if (voiceDuck != null)
            {
                AudioSystem.VoiceOver.DuckParameter = voiceDuck;
                AudioSystem.VoiceOver.DuckFadeSeconds = 0.25f;
            }

            AudioSystem.VoiceOver.SubtitleChanged += OnSubtitleChanged;

            SetIntensity(0f);

            if (track != null)
            {
                AudioSystem.PlayMusic(track);
            }
        }

        private void OnDestroy()
        {
            if (AudioRuntime.Exists)
            {
                AudioSystem.VoiceOver.SubtitleChanged -= OnSubtitleChanged;
            }
        }

        private void OnSubtitleChanged(string text)
        {
            subtitle = text;
        }

        public void SetIntensity(float value)
        {
            currentIntensity = Mathf.Clamp01(value);

            if (intensity != null)
            {
                AudioSystem.SetParameter(intensity, currentIntensity);
            }
        }

        public void StepIntensity(float direction)
        {
            SetIntensity(currentIntensity + direction * intensityStep);
        }

        public void FireStinger()
        {
            if (stinger != null)
            {
                AudioSystem.PlayStinger(stinger, MusicQuantization.Beat, 0.7f);
            }
        }

        public void PlayBlip()
        {
            AudioSystem.Play(blips);
        }

        public void PlayImpactAround()
        {
            Vector3 position = transform.position + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
            AudioSystem.PlayAtPosition(impact, position);
        }

        public void SpeakFirst()
        {
            AudioSystem.PlayVoiceLine(firstLine);
        }

        public void SpeakSecond()
        {
            AudioSystem.PlayVoiceLine(secondLine);
        }

        public void SpeakBoth()
        {
            AudioSystem.PlayVoiceLine(firstLine);
            AudioSystem.PlayVoiceLine(secondLine);
        }

        private void OnGUI()
        {
            const float width = 260f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 12f, 12f, width, 460f), GUI.skin.box);

            GUILayout.Label("AudioMW demo");
            GUILayout.Space(4f);

            GUILayout.Label("Intensity " + currentIntensity.ToString("F2"));
            float slider = GUILayout.HorizontalSlider(currentIntensity, 0f, 1f);

            if (!Mathf.Approximately(slider, currentIntensity))
            {
                SetIntensity(slider);
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("-"))
            {
                StepIntensity(-1f);
            }

            if (GUILayout.Button("+"))
            {
                StepIntensity(1f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            if (GUILayout.Button("Stinger on next beat"))
            {
                FireStinger();
            }

            if (GUILayout.Button("Random blip"))
            {
                PlayBlip();
            }

            if (GUILayout.Button("Spatial impact"))
            {
                PlayImpactAround();
            }

            GUILayout.Space(6f);

            if (GUILayout.Button("Voice line A"))
            {
                SpeakFirst();
            }

            if (GUILayout.Button("Voice line B"))
            {
                SpeakSecond();
            }

            if (GUILayout.Button("Queue both lines"))
            {
                SpeakBoth();
            }

            GUILayout.Space(6f);

            if (AudioRuntime.Exists)
            {
                MusicPlayer music = AudioSystem.Music;
                GUILayout.Label("bar " + music.Clock.GetBarIndex(AudioSettings.dspTime) +
                                "   beat " + music.Clock.GetBeatInBar(AudioSettings.dspTime));

                for (int i = 0; i < music.ChannelCount; i++)
                {
                    GUILayout.Label(music.GetLayerName(i) + "  " + music.GetLayerWeight(i).ToString("F2"));
                }

                GUILayout.Label("duck " + AudioSystem.VoiceOver.DuckValue.ToString("F2"));
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                GUILayout.Space(6f);
                GUILayout.Label("\"" + subtitle + "\"");
            }

            GUILayout.EndArea();
        }
    }
}
