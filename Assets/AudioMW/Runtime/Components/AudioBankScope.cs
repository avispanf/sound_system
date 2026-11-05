using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Audio Bank Scope")]
    [DefaultExecutionOrder(-1000)]
    public sealed class AudioBankScope : MonoBehaviour
    {
        [Tooltip("Retained while this component is enabled. Wait for IsReady before playing addressed events.")]
        [SerializeField] private SoundBank[] banks = new SoundBank[0];
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private readonly List<SoundBank> acquired = new List<SoundBank>();
        private bool acquiredAll;
        public SoundBank[] Banks
        {
            get { return banks; }
            set { Release(); banks = value ?? new SoundBank[0]; if (isActiveAndEnabled) Acquire(); }
        }
        public bool IsReady
        {
            get
            {
                if (!isActiveAndEnabled || !acquiredAll) return false;
                foreach (SoundBank bank in acquired) if (bank == null || !bank.IsFullyLoaded()) return false;
                return true;
            }
        }

        private void OnEnable() { Acquire(); }
        private void OnDisable() { Release(); }

        private void Acquire()
        {
            try
            {
                foreach (SoundBank bank in banks)
                {
                    if (bank == null || acquired.Contains(bank)) continue;
                    IDisposable lease = AudioSystem.AcquireBank(bank);
                    if (lease == null) { Release(); return; }
                    leases.Add(lease);
                    acquired.Add(bank);
                }
                acquiredAll = true;
            }
            catch { Release(); throw; }
        }

        private void Release()
        {
            acquiredAll = false;
            IDisposable[] owned = leases.ToArray();
            leases.Clear();
            acquired.Clear();
            foreach (IDisposable lease in owned) lease.Dispose();
        }
    }
}
