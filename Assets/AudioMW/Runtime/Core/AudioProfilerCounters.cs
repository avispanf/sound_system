#if AUDIOMW_PROFILING_CORE
using Unity.Profiling;
#endif

namespace AudioMW
{
    public static class AudioProfilerCounters
    {
#if AUDIOMW_PROFILING_CORE
        private static readonly ProfilerCounterValue<int> ActiveVoices =
            new ProfilerCounterValue<int>(ProfilerCategory.Audio, "AudioMW Active Voices", ProfilerMarkerDataUnit.Count);

        private static readonly ProfilerCounterValue<int> PooledVoices =
            new ProfilerCounterValue<int>(ProfilerCategory.Audio, "AudioMW Pooled Voices", ProfilerMarkerDataUnit.Count);

        private static readonly ProfilerCounterValue<int> VoiceSteals =
            new ProfilerCounterValue<int>(ProfilerCategory.Audio, "AudioMW Voice Steals", ProfilerMarkerDataUnit.Count);

        private static readonly ProfilerCounterValue<int> RejectedRequests =
            new ProfilerCounterValue<int>(ProfilerCategory.Audio, "AudioMW Rejected Requests", ProfilerMarkerDataUnit.Count);
#endif

        public static bool IsAvailable
        {
            get
            {
#if AUDIOMW_PROFILING_CORE
                return true;
#else
                return false;
#endif
            }
        }

        public static void Sample(AudioRuntime runtime)
        {
            if (runtime == null || runtime.Pool == null)
            {
                return;
            }

#if AUDIOMW_PROFILING_CORE
            ActiveVoices.Value = runtime.Pool.ActiveCount;
            PooledVoices.Value = runtime.Pool.TotalCount;
            VoiceSteals.Value = runtime.Pool.StealCount;
            RejectedRequests.Value = runtime.RejectedRequests;
#endif
        }
    }
}
