using System.Linq;
using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "MixProfile", menuName = "AudioMW/Mix Profile", order = 61)]
    public sealed class AudioMixProfile : ScriptableObject
    {
        [SerializeField] private MixerRoutingProfile routing;
        [SerializeField] private SoundParameter voiceDuck;
        [SerializeField, Min(0)] private float attackSeconds = 0.1f;
        [SerializeField, Min(0)] private float releaseSeconds = 0.4f;
        public MixerRoutingProfile Routing { get => routing; set => routing = value; }
        public SoundParameter VoiceDuck { get => voiceDuck; set => voiceDuck = value; }
        public float AttackSeconds { get => attackSeconds; set => attackSeconds = Mathf.Max(0, value); }
        public float ReleaseSeconds { get => releaseSeconds; set => releaseSeconds = Mathf.Max(0, value); }
        public bool IsUsable => routing != null && routing.IsUsable && voiceDuck != null && voiceDuck.IsGlobal
            && voiceDuck.MinValue == 0 && voiceDuck.MaxValue == 1 && voiceDuck.DefaultValue == 0
            && Finite(attackSeconds) && Finite(releaseSeconds) && attackSeconds >= 0 && releaseSeconds >= 0
            && routing.Bindings.All(binding => binding != null && binding.IsValid && binding.Parameter == voiceDuck
                && Finite(binding.OutputMin) && Finite(binding.OutputMax) && binding.OutputMin == 0 && binding.OutputMax <= 0
                && Mathf.Approximately(binding.Evaluate(0), 0))
            && routing.Bindings.Select(binding => binding.ExposedName).Distinct().Count() == routing.Bindings.Length;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
