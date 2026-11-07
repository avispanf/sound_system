using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public struct EventDebugRecord
    {
        public SoundEvent Event;
        public PlaybackRejectionInfo Rejection;
        public float Time;
        public string EventName;
        public string ProblemEventName;
        public PlaybackOutcome Outcome;
        public string ClipName;
        public Vector3 Position;
        public bool Attached;

        public bool HasGainCapture;
        public float BaseVolume;
        public float ParameterMultiplier;
        public float BlendWeight;
        public float OcclusionMultiplier;
        public float UnclampedVolume;
        public float FinalVolumeDb { get { return AudioGain.ToDecibels(FinalVolume); } }
        public bool WasVolumeLimited { get { return UnclampedVolume > 1f; } }
        public float FinalVolume;
        public float FinalPitch;

        public string DescribeOutcome()
        {
            if (Rejection.HasInstanceLimit) return string.Format("Already playing {0} instances on this {1}. The limit is {2}.", Rejection.CurrentCount, Rejection.Scope, Rejection.Limit);
            if (Rejection.HasCooldown) return string.Format("Cooldown has {0:0.###} s remaining at capture.", Rejection.RemainingSeconds);
            if (Rejection.HasVoiceCapacity) return string.Format("Needs {0} physical voices; policy can provide {1} of {2}.", Rejection.RequestedVoices, Rejection.AvailableVoices, Rejection.VoiceCapacity);
            string content = string.IsNullOrEmpty(ProblemEventName) ? "" : ProblemEventName + ": ";
            switch (Rejection.ContentIssue)
            {
                case PlaybackContentIssue.NoUsableSource: return content + "Playback has no usable clips or layers at capture. Check clips, weights and child content.";
                case PlaybackContentIssue.MissingSwitchParameter: return content + "No switch parameter was assigned at capture.";
                case PlaybackContentIssue.NoSwitchMatch: return content + string.Format("Switch value {0:0.###} selected state {1}; no non-null child matched at capture.", Rejection.SwitchValue, Rejection.SwitchState);
                case PlaybackContentIssue.CircularReference: return content + "Circular child reference at capture.";
                case PlaybackContentIssue.NestingLimit: return content + "Container nesting exceeded 32 levels at capture.";
            }
            switch (Outcome)
            {
                case PlaybackOutcome.RejectedNullEvent:
                    return "no event supplied";

                case PlaybackOutcome.RejectedNoClips:
                    return "event has no usable clips";

                case PlaybackOutcome.RejectedClipNotLoaded:
                    return "addressed clips are not loaded; load their sound bank first";
                case PlaybackOutcome.RejectedRuntimeStopping:
                    return "audio runtime is shutting down";
                case PlaybackOutcome.RejectedContainerGraph:
                    return "container has a circular reference or exceeds 32 levels";
                case PlaybackOutcome.RejectedCooldown:
                    return "Event cooldown has not elapsed.";
                case PlaybackOutcome.RejectedInstanceLimit:
                    return "Event, emitter or concurrency group limit reached.";
                case PlaybackOutcome.RejectedNoVoice:
                    return "voice capacity cannot fit this event under its limit policy";

                case PlaybackOutcome.RejectedNoValidLayers:
                    return "container resolves no playable layers; check clips and state selection";

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
                "base {0:G4} x params {1:G4} x blend {2:G4} x occlusion {3:G4} = {4:G4}{5}",
                BaseVolume, ParameterMultiplier, BlendWeight, OcclusionMultiplier, FinalVolume,
                WasVolumeLimited ? " (limited from " + UnclampedVolume.ToString("G4") + ")" : "");
        }
        public string DescribeVolumeDecibels()
        {
            if (Outcome != PlaybackOutcome.Played) return "not played";
            return string.Format("base {0} + params {1} + blend {2} + occlusion {3} → {4}{5}",
                Db(BaseVolume), Db(ParameterMultiplier), Db(BlendWeight), Db(OcclusionMultiplier), Db(FinalVolume),
                WasVolumeLimited ? " (limited to 0 dB)" : "");
        }

        private static string Db(float linear)
        {
            return linear > 0 ? AudioGain.ToDecibels(linear).ToString("F1") + " dB" : "−∞ dB";
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

        public void Record(SoundEvent soundEvent, PlaybackOutcome outcome, Voice voice, Vector3 position, bool attached, PlaybackRejectionInfo rejection = default(PlaybackRejectionInfo))
        {
            if (!enabled)
            {
                return;
            }

            EventDebugRecord record = new EventDebugRecord();
            record.Event = soundEvent;
            record.Rejection = rejection;
            record.ProblemEventName = rejection.ProblemEvent != null ? rejection.ProblemEvent.name : null;
            record.Time = Time.realtimeSinceStartup;
            record.EventName = soundEvent != null ? soundEvent.name : "(null)";
            record.Outcome = outcome;
            record.Position = position;
            record.Attached = attached;

            if (voice != null && voice.Source != null)
            {
                record.HasGainCapture = true;
                record.ClipName = voice.Source.clip != null ? voice.Source.clip.name : null;
                record.BaseVolume = voice.BaseVolume;
                record.ParameterMultiplier = voice.LastVolumeMultiplier;
                record.BlendWeight = voice.LastBlendWeight;
                record.OcclusionMultiplier = voice.OcclusionVolume;
                record.UnclampedVolume = record.BaseVolume * record.ParameterMultiplier * record.BlendWeight * record.OcclusionMultiplier;
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
