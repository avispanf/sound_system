using System;
using UnityEngine;

namespace AudioMW
{
    internal sealed class AudioMixSession
    {
        private readonly AudioRuntime runtime;
        private AudioMixProfile profile;
        private MixerRoutingProfile routing;
        private SoundParameter parameter;
        private SoundParameter previousParameter;
        private float previousAttack;
        private float previousRelease;
        private float attack;
        private float release;
        private int references;
        private int generation;
        public AudioMixProfile Profile => profile;
        public AudioMixSession(AudioRuntime owner) { runtime = owner; }
        public IDisposable Acquire(AudioMixProfile requested)
        {
            if (requested == null || !requested.IsUsable || requested.Routing.FindMissingExposedParameters().Count > 0) throw new InvalidOperationException("Choose a valid mix profile with exposed mixer parameters.");
            if (profile != null && profile != requested) throw new InvalidOperationException("Another mix profile owns this runtime. Overlapping scenes must share one profile.");
            if (references == 0)
            {
                if (runtime.Mixing.ConflictsWith(requested.Routing)) throw new InvalidOperationException("The mix duck parameters already have a routing owner.");
                profile = requested; routing = UnityEngine.Object.Instantiate(profile.Routing); routing.hideFlags = HideFlags.HideAndDontSave; parameter = profile.VoiceDuck;
                VoiceOverDirector voice = runtime.VoiceOver;
                previousParameter = voice.DuckParameter; previousAttack = voice.DuckAttackSeconds; previousRelease = voice.DuckReleaseSeconds;
                attack = profile.AttackSeconds; release = profile.ReleaseSeconds;
                voice.DuckParameter = parameter; voice.DuckAttackSeconds = attack; voice.DuckReleaseSeconds = release;
                runtime.GlobalParameters.Set(parameter, voice.DuckValue); runtime.Mixing.AddProfile(routing);
            }
            references++;
            return new Lease(this, generation);
        }
        private sealed class Lease : IDisposable
        {
            private AudioMixSession owner;
            private readonly int generation;
            public Lease(AudioMixSession session, int version) { owner = session; generation = version; }
            public void Dispose()
            {
                AudioMixSession session = owner; owner = null;
                if (session != null && session.generation == generation && session.references > 0 && --session.references == 0) session.Clear();
            }
        }
        public void Clear()
        {
            generation++;
            if (routing != null)
            {
                runtime.Mixing.RemoveProfile(routing);
                if (routing.Mixer != null) foreach (MixerParameterBinding binding in routing.Bindings)
                    if (binding != null && binding.IsValid) routing.Mixer.ClearFloat(binding.ExposedName);
            }
            VoiceOverDirector voice = runtime.VoiceOver;
            if (profile != null && voice.DuckParameter == parameter)
            {
                voice.DuckParameter = previousParameter;
                if (Mathf.Approximately(voice.DuckAttackSeconds, attack)) voice.DuckAttackSeconds = previousAttack;
                if (Mathf.Approximately(voice.DuckReleaseSeconds, release)) voice.DuckReleaseSeconds = previousRelease;
            }
            if (parameter != null) runtime.GlobalParameters.Set(parameter, 0);
            if (routing != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(routing); else UnityEngine.Object.DestroyImmediate(routing);
            }
            profile = null; routing = null; parameter = null; references = 0;
        }
    }
}
