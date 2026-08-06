using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public struct EventDebugRecord
    {
        public float Time;
        public string EventName;
        public PlaybackOutcome Outcome;
        public string ClipName;
        public Vector3 Position;
        public bool Attached;

        public float BaseVolume;
        public float ParameterMultiplier;
        public float BlendWeight;
        public float FinalVolume;
        public float FinalPitch;

        public string DescribeOutcome()
        {
            switch (Outcome)
            {
                case PlaybackOutcome.RejectedNullEvent:
                    return "no event supplied";

                case PlaybackOutcome.RejectedNoClips:
                    return "event has no usable clips";

                case PlaybackOutcome.RejectedNoVoice:
                    return "voice limit reached, nothing to steal";

                case PlaybackOutcome.RejectedNoValidLayers:
                    return "blend container has no valid layers";

                default:
                    return "played";
            }
        }

        public string DescribeVolumeChain()
        {
            if (Outcome != PlaybackOutcome.Played)
            {
                return "not played";
            }

            return string.Format(
                "base {0:F2} x params {1:F2} x blend {2:F2} = {3:F2}",
                BaseVolume,
                ParameterMultiplier,
                BlendWeight,
                FinalVolume);
        }
    }

    public sealed class EventDebugger
    {
        public const int DefaultCapacity = 256;

        private readonly List<EventDebugRecord> records = new List<EventDebugRecord>();
        private int capacity = DefaultCapacity;
        private bool enabled = true;

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public int Capacity
        {
            get { return capacity; }
            set
            {
                capacity = Mathf.Max(1, value);
                Trim();
            }
        }

        public int Count
        {
            get { return records.Count; }
        }

        public IReadOnlyList<EventDebugRecord> Records
        {
            get { return records; }
        }

        public void Clear()
        {
            records.Clear();
        }

        public void Record(SoundEvent soundEvent, PlaybackOutcome outcome, Voice voice, Vector3 position, bool attached)
        {
            if (!enabled)
            {
                return;
            }

            EventDebugRecord record = new EventDebugRecord();
            record.Time = Time.realtimeSinceStartup;
            record.EventName = soundEvent != null ? soundEvent.name : "(null)";
            record.Outcome = outcome;
            record.Position = position;
            record.Attached = attached;

            if (voice != null && voice.Source != null)
            {
                record.ClipName = voice.Source.clip != null ? voice.Source.clip.name : null;
                record.BaseVolume = voice.BaseVolume;
                record.ParameterMultiplier = voice.LastVolumeMultiplier;
                record.BlendWeight = voice.LastBlendWeight;
                record.FinalVolume = voice.Source.volume;
                record.FinalPitch = voice.Source.pitch;
            }

            records.Add(record);
            Trim();
        }

        public int CountWithOutcome(PlaybackOutcome outcome)
        {
            int total = 0;

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Outcome == outcome)
                {
                    total++;
                }
            }

            return total;
        }

        public bool TryGetLast(out EventDebugRecord record)
        {
            if (records.Count == 0)
            {
                record = new EventDebugRecord();
                return false;
            }

            record = records[records.Count - 1];
            return true;
        }

        public List<EventDebugRecord> Filter(string eventNameFilter, bool rejectionsOnly)
        {
            List<EventDebugRecord> result = new List<EventDebugRecord>();
            bool hasFilter = !string.IsNullOrEmpty(eventNameFilter);

            for (int i = 0; i < records.Count; i++)
            {
                EventDebugRecord record = records[i];

                if (rejectionsOnly && record.Outcome == PlaybackOutcome.Played)
                {
                    continue;
                }

                if (hasFilter && (record.EventName == null ||
                    record.EventName.IndexOf(eventNameFilter, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                result.Add(record);
            }

            return result;
        }

        private void Trim()
        {
            int excess = records.Count - capacity;

            if (excess > 0)
            {
                records.RemoveRange(0, excess);
            }
        }
    }
}
