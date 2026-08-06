using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "VO_NewLine", menuName = "AudioMW/Voice Line", order = 40)]
    public sealed class VoiceLine : ScriptableObject
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string speaker;
        [SerializeField, TextArea(2, 6)] private string subtitle;
        [SerializeField] private AudioMixerGroup mixerGroup;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private int priority;
        [SerializeField] private float trailingSilence = 0.15f;

        public AudioClip Clip
        {
            get { return clip; }
            set { clip = value; }
        }

        public string Speaker
        {
            get { return speaker; }
            set { speaker = value; }
        }

        public string Subtitle
        {
            get { return subtitle; }
            set { subtitle = value; }
        }

        public AudioMixerGroup MixerGroup
        {
            get { return mixerGroup; }
            set { mixerGroup = value; }
        }

        public float Volume
        {
            get { return volume; }
            set { volume = Mathf.Clamp01(value); }
        }

        public int Priority
        {
            get { return priority; }
            set { priority = value; }
        }

        public float TrailingSilence
        {
            get { return Mathf.Max(0f, trailingSilence); }
            set { trailingSilence = Mathf.Max(0f, value); }
        }

        public bool IsValid
        {
            get { return clip != null; }
        }

        public double Duration
        {
            get { return MusicTrack.GetExactDuration(clip); }
        }

        public static VoiceLine CreateRuntime(AudioClip clip, string speaker, string subtitle)
        {
            VoiceLine line = CreateInstance<VoiceLine>();
            line.clip = clip;
            line.speaker = speaker;
            line.subtitle = subtitle;
            return line;
        }
    }
}
