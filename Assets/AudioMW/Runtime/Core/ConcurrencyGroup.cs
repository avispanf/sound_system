using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "Concurrency", menuName = "AudioMW/Concurrency Group", order = 15)]
    public sealed class ConcurrencyGroup : ScriptableObject
    {
        [SerializeField, Min(1)] private int maxInstances = 8;
        [SerializeField] private VoiceLimitPolicy policy = VoiceLimitPolicy.StealOldest;
        public int MaxInstances { get { return Mathf.Max(1, maxInstances); } set { maxInstances = Mathf.Max(1, value); } }
        public VoiceLimitPolicy Policy { get { return policy; } set { policy = value; } }
    }
}
