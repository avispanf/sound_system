using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class ClipReference
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string address;

        [NonSerialized] private AudioClip resolved;

        [NonSerialized] private Dictionary<object, AudioClip> owners;

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
            get { return clip == null && !string.IsNullOrWhiteSpace(address); }
        }

        public bool IsResolved
        {
            get { return Clip != null; }
        }

        public AudioClip Clip
        {
            get
            {
                if (clip != null) return clip;
                if (resolved != null) return resolved;
                if (owners != null) foreach (AudioClip owned in owners.Values) if (owned != null) return owned;
                return null;
            }
        }

        public void SetResolved(AudioClip value)
        {
            resolved = value;
        }

        public void ClearResolved()
        {
            resolved = null;
            if (owners != null) owners.Clear();
        }

        internal void ResolveFor(object owner, AudioClip value)
        {
            if (owners == null) owners = new Dictionary<object, AudioClip>();
            owners[owner] = value;
        }

        internal void ReleaseFor(object owner)
        {
            if (owners != null) owners.Remove(owner);
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
