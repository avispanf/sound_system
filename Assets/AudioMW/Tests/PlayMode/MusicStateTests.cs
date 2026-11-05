using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class MusicStateTests
    {
        private MusicStateProfile profile;
        private MusicTrack exploration;
        private MusicTrack combat;
        private AudioClip clip;

        [SetUp]
        public void SetUp()
        {
            AudioSystem.StopMusic();
            clip = AudioTestUtil.MakeSine(1f);
            exploration = MusicTrack.CreateRuntime(null, clip, 120, 4);
            combat = MusicTrack.CreateRuntime(null, clip, 90, 4);
            exploration.Volume = combat.Volume = 0.05f;
            profile = ScriptableObject.CreateInstance<MusicStateProfile>();
            profile.States = new[] { new MusicState { Name = "Explore", Track = exploration }, new MusicState { Name = "Combat", Track = combat } };
            profile.Transitions = new[] { new MusicTransitionRule { From = "Explore", To = "Combat", Quantization = MusicQuantization.Immediate } };
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopMusic();
            Object.Destroy(profile); Object.Destroy(exploration); Object.Destroy(combat); Object.Destroy(clip);
        }

        [UnityTest]
        public IEnumerator AuthoredRuleOverridesDefaultBarTiming()
        {
            Assert.IsTrue(profile.Request(AudioSystem.Music, "Explore"));
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(profile.Request(AudioSystem.Music, "Combat"));
            Assert.Less(AudioSystem.Music.PendingStartDspTime - AudioSettings.dspTime, 0.1);
            yield return AudioTestUtil.WaitUntil(() => AudioSystem.Music.CurrentTrack == combat, "authored music transition");
            Assert.AreEqual("Combat", profile.StateFor(AudioSystem.Music.CurrentTrack));
        }

        [UnityTest]
        public IEnumerator RepeatedRequestDoesNotReschedulePendingState()
        {
            profile.Request(AudioSystem.Music, "Explore");
            profile.Transitions = new MusicTransitionRule[0];
            profile.Request(AudioSystem.Music, "Combat");
            double pending = AudioSystem.Music.PendingStartDspTime;
            yield return null;
            profile.Request(AudioSystem.Music, "Combat");
            Assert.AreEqual(pending, AudioSystem.Music.PendingStartDspTime);
        }

        [UnityTest]
        public IEnumerator UnknownStateDoesNotDisturbCurrentMusic()
        {
            profile.Request(AudioSystem.Music, "Explore");
            Assert.IsFalse(profile.Request(AudioSystem.Music, "Missing"));
            Assert.AreSame(exploration, AudioSystem.Music.CurrentTrack);
            Assert.IsFalse(AudioSystem.Music.IsTransitionPending);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateNamesAndMissingRoutesAreRejected()
        {
            profile.States[1].Name = "Explore";
            Assert.IsFalse(profile.Request(AudioSystem.Music, "Explore"));
            StringAssert.Contains("unique", profile.Validate());
            profile.States[1].Name = "Combat";
            profile.Transitions[0].To = "Missing";
            Assert.IsFalse(profile.Request(AudioSystem.Music, "Explore"));
            Assert.IsFalse(AudioSystem.Music.IsPlaying);
            yield return null;
        }
    }
}
