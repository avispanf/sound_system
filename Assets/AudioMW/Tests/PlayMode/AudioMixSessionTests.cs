#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class AudioMixSessionTests
    {
        private AudioMixProfile profile;
        private SoundParameter duck;
        private MixerRoutingProfile routing;
        private AudioMixer mixer;
        private IDisposable lease;
        [SetUp] public void SetUp()
        {
            AudioSystem.StopVoiceOver();
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath("8f28d703cd8d407d95180230c1356afe"));
            duck = SoundParameter.CreateRuntime(0, 1, 0);
            routing = MixerRoutingProfile.CreateRuntime(mixer, MixerParameterBinding.CreateRuntime(duck, "AudioMW_MusicDuckDb", null, 0, -6), MixerParameterBinding.CreateRuntime(duck, "AudioMW_AmbienceDuckDb", null, 0, -9));
            profile = ScriptableObject.CreateInstance<AudioMixProfile>(); profile.Routing = routing; profile.VoiceDuck = duck; profile.AttackSeconds = 0.05f; profile.ReleaseSeconds = 0.1f;
        }
        [TearDown] public void TearDown()
        {
            AudioSystem.StopVoiceOver(); lease?.Dispose(); lease = null;
            mixer.ClearFloat("AudioMW_MusicDuckDb"); mixer.ClearFloat("AudioMW_AmbienceDuckDb");
            Object.DestroyImmediate(profile); Object.DestroyImmediate(routing); Object.DestroyImmediate(duck);
        }
        [UnityTest] public IEnumerator SpeechDrivesRealBusGainAndReleaseReturnsToNeutral()
        {
            yield return null;
            lease = AudioRuntime.Instance.AcquireMixProfile(profile);
            AudioClip clip = AudioClip.Create("Speech", 44100, 1, 44100, false); VoiceLine line = VoiceLine.CreateRuntime(clip, "Guide", "Test");
            try
            {
                Assert.IsTrue(AudioSystem.PlayVoiceLine(line));
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.AreEqual(1, AudioSystem.GetParameter(duck), 0.01f);
                Assert.IsTrue(mixer.GetFloat("AudioMW_MusicDuckDb", out float value)); Assert.AreEqual(-6, value, 0.01f);
                AudioSystem.StopVoiceOver(); yield return new WaitForSecondsRealtime(0.2f);
                Assert.AreEqual(0, AudioSystem.GetParameter(duck), 0.01f);
                mixer.GetFloat("AudioMW_MusicDuckDb", out value); Assert.AreEqual(0, value, 0.01f);
            }
            finally { AudioSystem.StopVoiceOver(); Object.DestroyImmediate(line); Object.DestroyImmediate(clip); }
        }
        [UnityTest] public IEnumerator SharedLeasesRetainOwnershipAndRestorePreviousEnvelope()
        {
            yield return null;
            SoundParameter previous = AudioSystem.VoiceOver.DuckParameter; float attack = AudioSystem.VoiceOver.DuckAttackSeconds; float release = AudioSystem.VoiceOver.DuckReleaseSeconds;
            lease = AudioRuntime.Instance.AcquireMixProfile(profile); IDisposable second = AudioRuntime.Instance.AcquireMixProfile(profile);
            lease.Dispose(); Assert.AreSame(profile, AudioRuntime.Instance.ActiveMixProfile);
            second.Dispose(); Assert.IsNull(AudioRuntime.Instance.ActiveMixProfile); Assert.AreSame(previous, AudioSystem.VoiceOver.DuckParameter);
            Assert.AreEqual(attack, AudioSystem.VoiceOver.DuckAttackSeconds); Assert.AreEqual(release, AudioSystem.VoiceOver.DuckReleaseSeconds);
            Assert.AreEqual(0, AudioRuntime.Instance.Mixing.ProfileCount);
        }
        [UnityTest] public IEnumerator ConflictingOwnerAndInvalidEnvelopeCannotAcquire()
        {
            yield return null;
            AudioRuntime.Instance.Mixing.AddProfile(routing);
            Assert.Throws<InvalidOperationException>(() => AudioRuntime.Instance.AcquireMixProfile(profile)); AudioRuntime.Instance.Mixing.RemoveProfile(routing);
            lease = AudioRuntime.Instance.AcquireMixProfile(profile);
            AudioMixProfile other = Object.Instantiate(profile);
            try { Assert.Throws<InvalidOperationException>(() => AudioRuntime.Instance.AcquireMixProfile(other)); }
            finally { Object.DestroyImmediate(other); }
            lease.Dispose(); profile.Routing = null;
            Assert.Throws<InvalidOperationException>(() => AudioRuntime.Instance.AcquireMixProfile(profile));
        }
        [UnityTest] public IEnumerator IdenticalExposedNamesOnDifferentMixersReceiveIndependentWrites()
        {
            yield return null;
            string folder = "Assets/MixerCache_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            string template = AssetDatabase.GUIDToAssetPath("8f28d703cd8d407d95180230c1356afe");
            Assert.IsTrue(AssetDatabase.CopyAsset(template, folder + "/A.mixer"));
            Assert.IsTrue(AssetDatabase.CopyAsset(template, folder + "/B.mixer"));
            AudioMixer a = AssetDatabase.LoadAssetAtPath<AudioMixer>(folder + "/A.mixer"), b = AssetDatabase.LoadAssetAtPath<AudioMixer>(folder + "/B.mixer");
            yield return null;
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 1, 0);
            MixerRoutingProfile first = MixerRoutingProfile.CreateRuntime(a, MixerParameterBinding.CreateRuntime(parameter, "AudioMW_MusicDuckDb", null, -6, -6));
            MixerRoutingProfile second = MixerRoutingProfile.CreateRuntime(b, MixerParameterBinding.CreateRuntime(parameter, "AudioMW_MusicDuckDb", null, -6, -6));
            try
            {
                MixerDirector director = new MixerDirector(); director.AddProfile(first); director.AddProfile(second); director.Tick(); Assert.AreEqual(2, director.WritesLastTick);
                Assert.IsTrue(a.GetFloat("AudioMW_MusicDuckDb", out float gainA)); Assert.AreEqual(-6, gainA);
                Assert.IsTrue(b.GetFloat("AudioMW_MusicDuckDb", out float gainB)); Assert.AreEqual(-6, gainB);
                director.Tick(); Assert.AreEqual(0, director.WritesLastTick);
            }
            finally { a.ClearFloat("AudioMW_MusicDuckDb"); b.ClearFloat("AudioMW_MusicDuckDb"); Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(parameter); AssetDatabase.DeleteAsset(folder); }
        }
        [UnityTest] public IEnumerator ShutdownInvalidatesOldLeasesWithoutReleasingNewSession()
        {
            yield return null;
            IDisposable old = AudioRuntime.Instance.AcquireMixProfile(profile); AudioSystem.Shutdown();
            lease = AudioRuntime.Instance.AcquireMixProfile(profile); old.Dispose();
            Assert.AreSame(profile, AudioRuntime.Instance.ActiveMixProfile); Assert.AreEqual(1, AudioRuntime.Instance.Mixing.ProfileCount);
        }
        [Test] public void LegacyFadeSetterStillConfiguresBothDirections()
        {
            AudioSystem.VoiceOver.DuckFadeSeconds = 0.2f;
            Assert.AreEqual(0.2f, AudioSystem.VoiceOver.DuckAttackSeconds); Assert.AreEqual(0.2f, AudioSystem.VoiceOver.DuckReleaseSeconds);
        }
    }
}
#endif
