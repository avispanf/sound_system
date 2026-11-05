using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class StreamedEventTests
    {
        private sealed class Loader : CachedAudioAssetLoader
        {
            public Action<AudioClip> Complete;
            public bool CleanOnRelease;
            protected override void BeginLoad(string address, Action<AudioClip> complete) { Complete = complete; }
            protected override void EndLoad(string address)
            {
                CleanOnRelease = AudioSystem.ActiveVoiceCount == 0 && !AudioSystem.Music.IsPlaying && !AudioSystem.VoiceOver.IsSpeaking;
            }
        }
        private readonly List<Object> assets = new List<Object>();
        private IAudioAssetLoader previous;
        [SetUp] public void SetUp() { AudioSystem.Shutdown(); previous = AudioSystem.AssetLoader; }
        [TearDown] public void TearDown()
        {
            AudioSystem.Shutdown(); AudioSystem.AssetLoader = previous;
            foreach (Object item in assets) if (item != null) Object.Destroy(item); assets.Clear();
        }
        private AudioClip Clip() { AudioClip clip = AudioTestUtil.MakeSine(1); assets.Add(clip); return clip; }
        private SoundEvent Event(params AudioClip[] clips)
        {
            SoundEvent item = SoundEvent.CreateRuntime(clips); item.Loop = true; item.SpatialBlend = 0; assets.Add(item); return item;
        }
        [UnityTest] public IEnumerator NestedEventWaitsForBankAndThenPlaysAllLayers()
        {
            Loader loader = new Loader(); AudioSystem.AssetLoader = loader;
            SoundEvent addressed = Event(); addressed.ClipReferences = new[] { ClipReference.Addressed("layer") };
            SoundEvent root = Event(); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(Event(Clip())), SoundEventChild.Create(addressed) };
            SoundBank bank = SoundBank.CreateRuntime(root); assets.Add(bank);
            AudioSystem.LoadBank(bank);
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned); Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.RejectedClipNotLoaded, record.Outcome);
            loader.Complete(Clip());
            SoundHandle handle = AudioSystem.PlayTracked(root); Assert.IsTrue(handle.IsPlaying); Assert.AreEqual(2, AudioSystem.ActiveVoiceCount);
            handle.Stop(); AudioSystem.UnloadBank(bank);
            Assert.IsFalse(addressed.ClipReferences[0].IsResolved);
            yield return null;
        }
        [UnityTest] public IEnumerator ShutdownStopsPlaybackBeforeReleasingStreamedAssets()
        {
            Loader loader = new Loader(); AudioSystem.AssetLoader = loader;
            SoundEvent item = Event(); item.ClipReferences = new[] { ClipReference.Addressed("shared") };
            SoundBank bank = SoundBank.CreateRuntime(item); assets.Add(bank); AudioSystem.LoadBank(bank);
            AudioClip clip = Clip(); loader.Complete(clip); AudioSystem.PlayTracked(item);
            MusicTrack track = MusicTrack.CreateRuntime(null, clip, 120, 4); assets.Add(track); AudioSystem.PlayMusic(track);
            VoiceLine line = VoiceLine.CreateRuntime(clip, "Stream", "Shutdown"); assets.Add(line); AudioSystem.PlayVoiceLine(line);
            AudioSystem.Shutdown();
            Assert.IsTrue(loader.CleanOnRelease); Assert.AreEqual(0, loader.RetainedAddressCount); Assert.IsFalse(bank.IsLoaded);
            yield return null;
        }

        [UnityTest] public IEnumerator ShutdownRejectsBankAcquisitionFromReleaseCallback()
        {
            Loader loader = new Loader(); AudioSystem.AssetLoader = loader;
            SoundEvent loading = Event(); loading.ClipReferences = new[] { ClipReference.Addressed("late") };
            SoundBank bank = SoundBank.CreateRuntime(loading); assets.Add(bank);
            SoundHandle active = AudioSystem.PlayTracked(Event(Clip()));
            active.Voice.Released += _ => AudioSystem.LoadBank(bank);
            AudioSystem.Shutdown();
            Assert.IsFalse(bank.IsLoaded); Assert.AreEqual(0, bank.PendingStreamCount); Assert.IsNull(loader.Complete);
            yield return null;
        }
    }
}
