using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "MIX_NewRouting", menuName = "AudioMW/Mixer Routing", order = 60)]
    public sealed class MixerRoutingProfile : ScriptableObject
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private MixerParameterBinding[] bindings = new MixerParameterBinding[0];
        [SerializeField] private string[] expectedExposedNames = new string[0];

        public AudioMixer Mixer
        {
            get { return mixer; }
            set { mixer = value; }
        }

        public MixerParameterBinding[] Bindings
        {
            get { return bindings; }
            set { bindings = value ?? new MixerParameterBinding[0]; }
        }

        public string[] ExpectedExposedNames
        {
            get { return expectedExposedNames; }
            set { expectedExposedNames = value ?? new string[0]; }
        }

        public bool IsUsable
        {
            get { return mixer != null && bindings != null && bindings.Length > 0; }
        }

        public List<string> FindMissingExposedParameters()
        {
            List<string> missing = new List<string>();

            if (mixer == null)
            {
                return missing;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                MixerParameterBinding binding = bindings[i];

                if (binding == null || !binding.IsValid)
                {
                    continue;
                }

                if (!HasExposedParameter(binding.ExposedName) && !missing.Contains(binding.ExposedName))
                {
                    missing.Add(binding.ExposedName);
                }
            }

            for (int i = 0; i < expectedExposedNames.Length; i++)
            {
                string name = expectedExposedNames[i];

                if (!string.IsNullOrEmpty(name) && !HasExposedParameter(name) && !missing.Contains(name))
                {
                    missing.Add(name);
                }
            }

            return missing;
        }

        public bool HasExposedParameter(string exposedName)
        {
            if (mixer == null || string.IsNullOrEmpty(exposedName))
            {
                return false;
            }

            float value;
            return mixer.GetFloat(exposedName, out value);
        }

        public static MixerRoutingProfile CreateRuntime(AudioMixer mixer, params MixerParameterBinding[] bindings)
        {
            MixerRoutingProfile profile = CreateInstance<MixerRoutingProfile>();
            profile.mixer = mixer;
            profile.bindings = bindings ?? new MixerParameterBinding[0];
            return profile;
        }
    }
}
