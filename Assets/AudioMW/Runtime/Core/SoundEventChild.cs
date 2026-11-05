using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class SoundEventChild
    {
        [SerializeField] private SoundEvent soundEvent;
        [SerializeField] private string label;
        [SerializeField] private int state;
        [SerializeField, DecibelGain] private float gain = 1f;

        public SoundEvent Event { get { return soundEvent; } set { soundEvent = value; } }
        public string Label { get { return label; } set { label = value; } }
        public int State { get { return state; } set { state = value; } }
        public float GainDb { get { return AudioGain.ToDecibels(gain); } set { Gain = AudioGain.FromDecibels(value); } }
        public float Gain { get { return gain; } set { gain = Mathf.Clamp01(value); } }

        public static SoundEventChild Create(SoundEvent soundEvent, int state = 0, float gain = 1f)
        {
            return new SoundEventChild { Event = soundEvent, State = state, Gain = gain };
        }
    }
}
