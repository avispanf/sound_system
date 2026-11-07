using UnityEngine;

namespace AudioMW.Editor
{
    public sealed class AudioMixInstallation : ScriptableObject
    {
        [SerializeField] private string signature;
        [SerializeField] private string appliedState;
        [SerializeField] private string[] files;
        [SerializeField] private AudioMixProfile profile;
        public string Signature => signature;
        public string AppliedState => appliedState;
        public string[] Files => (string[])files.Clone();
        public AudioMixProfile Profile => profile;
        internal void Record(string request, string state, string[] paths, AudioMixProfile mix)
        {
            signature = request; appliedState = state; files = paths; profile = mix;
        }
    }
}
