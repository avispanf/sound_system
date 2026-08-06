using UnityEngine;

namespace AudioMW
{
    public struct PlaybackParameters
    {
        public AudioClip Clip;
        public float Volume;
        public float Pitch;
        public bool IsValid;

        public static PlaybackParameters Invalid
        {
            get { return new PlaybackParameters { IsValid = false }; }
        }
    }
}
