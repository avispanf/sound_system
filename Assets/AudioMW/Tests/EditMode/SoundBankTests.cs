using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class SoundBankTests
    {
        [Test]
        public void CollectsClipsFromSimpleEvent()
        {
            AudioClip a = MakeClip("a");
            AudioClip b = MakeClip("b");
            SoundEvent soundEvent = SoundEvent.CreateRuntime(a, b);

            List<AudioClip> collected = new List<AudioClip>();
            SoundBank.CollectClips(soundEvent, collected);

            Assert.AreEqual(2, collected.Count);
            CollectionAssert.Contains(collected, a);
            CollectionAssert.Contains(collected, b);
        }

        [Test]
        public void CollectsClipsFromBlendLayers()
        {
            AudioClip layerClip = MakeClip("layer");
            SoundEvent soundEvent = SoundEvent.CreateRuntime();
            soundEvent.ContainerMode = ContainerMode.Blend;
            soundEvent.BlendLayers = new[] { BlendLayer.CreateRuntime(layerClip, null) };

            List<AudioClip> collected = new List<AudioClip>();
            SoundBank.CollectClips(soundEvent, collected);

            Assert.AreEqual(1, collected.Count);
            CollectionAssert.Contains(collected, layerClip);
        }

        [Test]
        public void CollectionDeduplicatesSharedClips()
        {
            AudioClip shared = MakeClip("shared");
            SoundEvent first = SoundEvent.CreateRuntime(shared);
            SoundEvent second = SoundEvent.CreateRuntime(shared);

            List<AudioClip> collected = new List<AudioClip>();
            SoundBank.CollectClips(first, collected);
            SoundBank.CollectClips(second, collected);

            Assert.AreEqual(1, collected.Count);
        }

        [Test]
        public void CollectionIgnoresNulls()
        {
            List<AudioClip> collected = new List<AudioClip>();
            SoundBank.CollectClips(null, collected);
            SoundBank.CollectClips(SoundEvent.CreateRuntime(null, null), collected);

            Assert.AreEqual(0, collected.Count);
        }

        [Test]
        public void LoadMarksBankLoadedAndTracksClips()
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeClip("one"), MakeClip("two"));
            SoundBank bank = SoundBank.CreateRuntime(soundEvent);

            Assert.IsFalse(bank.IsLoaded);

            bank.Load();

            Assert.IsTrue(bank.IsLoaded);
            Assert.AreEqual(2, bank.LoadedClipCount);
        }

        [Test]
        public void LoadIsIdempotent()
        {
            SoundBank bank = SoundBank.CreateRuntime(SoundEvent.CreateRuntime(MakeClip("one")));

            bank.Load();
            bank.Load();

            Assert.AreEqual(1, bank.LoadedClipCount);
        }

        [Test]
        public void UnloadClearsTrackedClips()
        {
            SoundBank bank = SoundBank.CreateRuntime(SoundEvent.CreateRuntime(MakeClip("one")));

            bank.Load();
            bank.Unload();

            Assert.IsFalse(bank.IsLoaded);
            Assert.AreEqual(0, bank.LoadedClipCount);
        }

        [Test]
        public void UnloadOnUnloadedBankIsSafe()
        {
            SoundBank bank = SoundBank.CreateRuntime();

            Assert.DoesNotThrow(bank.Unload);
            Assert.IsFalse(bank.IsLoaded);
        }

        private static AudioClip MakeClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 1024, 1, 44100, false);
            clip.SetData(new float[1024], 0);
            return clip;
        }
    }
}
