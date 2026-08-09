using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class MusicMarker
    {
        [SerializeField] private string markerName = "Marker";
        [SerializeField, Min(0)] private int bar;
        [SerializeField, Min(0)] private int beat;

        public string Name
        {
            get { return markerName; }
        }

        public int Bar
        {
            get { return Mathf.Max(0, bar); }
        }

        public int Beat
        {
            get { return Mathf.Max(0, beat); }
        }

        public double PositionInBeats(int beatsPerBar)
        {
            return Bar * Mathf.Max(1, beatsPerBar) + Beat;
        }

        public static MusicMarker CreateRuntime(string name, int bar, int beat)
        {
            return new MusicMarker
            {
                markerName = name,
                bar = Mathf.Max(0, bar),
                beat = Mathf.Max(0, beat)
            };
        }
    }
}
