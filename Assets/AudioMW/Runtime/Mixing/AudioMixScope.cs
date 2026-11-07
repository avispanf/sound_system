using System;
using UnityEngine;

namespace AudioMW
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class AudioMixScope : MonoBehaviour
    {
        [SerializeField] private AudioMixProfile profile;
        private IDisposable lease;
        public AudioMixProfile Profile { get => profile; set => profile = value; }
        private void Start()
        {
            if (profile != null) lease = AudioRuntime.Instance.AcquireMixProfile(profile);
        }
        private void OnDestroy() { lease?.Dispose(); lease = null; }
    }
}
