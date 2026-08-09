using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class ClipReference
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string address;

        [NonSerialized] private AudioClip resolved;

        public AudioClip DirectClip
        {
            get { return clip; }
        }

        public string Address
        {
            get { return address; }
        }

        public bool IsAddressed
        {
            get { return clip == null && !string.IsNullOrEmpty(address); }
        }

        public bool IsResolved
        {
            get { return Clip != null; }
        }

        public AudioClip Clip
        {
            get { return clip != null ? clip : resolved; }
        }

        public void SetResolved(AudioClip value)
        {
            resolved = value;
        }

        public void ClearResolved()
        {
            resolved = null;
        }

        public static ClipReference Direct(AudioClip clip)
        {
            return new ClipReference { clip = clip };
        }

        public static ClipReference Addressed(string address)
        {
            return new ClipReference { address = address };
        }
    }
}
