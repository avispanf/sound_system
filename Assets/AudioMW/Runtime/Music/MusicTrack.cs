using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "MUS_NewTrack", menuName = "AudioMW/Music Track", order = 30)]
    public sealed class MusicTrack : ScriptableObject
    {
        [SerializeField] private AudioClip introClip;
        [SerializeField] private AudioClip loopClip;
        [SerializeField] private bool loop = true;

        [SerializeField] private double tempo = 120.0;
        [SerializeField] private int beatsPerBar = 4;

        [SerializeField] private AudioMixerGroup mixerGroup;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        public AudioClip IntroClip
        {
            get { return introClip; }
            set { introClip = value; }
        }

        public AudioClip LoopClip
        {
            get { return loopClip; }
            set { loopClip = value; }
        }

        public bool Loop
        {
            get { return loop; }
            set { loop = value; }
        }

        public double Tempo
        {
            get { return tempo; }
            set { tempo = System.Math.Max(1.0, value); }
        }

        public int BeatsPerBar
        {
            get { return beatsPerBar; }
            set { beatsPerBar = Mathf.Max(1, value); }
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

        public bool HasContent
        {
            get { return introClip != null || loopClip != null; }
        }

        public static double GetExactDuration(AudioClip clip)
        {
            if (clip == null || clip.frequency <= 0)
            {
                return 0.0;
            }

            return (double)clip.samples / clip.frequency;
        }

        public double IntroDuration
        {
            get { return GetExactDuration(introClip); }
        }

        public double LoopDuration
        {
            get { return GetExactDuration(loopClip); }
        }

        public static MusicTrack CreateRuntime(AudioClip intro, AudioClip loopBody, double bpm, int signature)
        {
            MusicTrack track = CreateInstance<MusicTrack>();
            track.introClip = intro;
            track.loopClip = loopBody;
            track.tempo = System.Math.Max(1.0, bpm);
            track.beatsPerBar = Mathf.Max(1, signature);
            return track;
        }
    }
}
