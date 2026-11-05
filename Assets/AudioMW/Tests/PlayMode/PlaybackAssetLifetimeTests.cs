using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class PlaybackAssetLifetimeTests
    {
        private sealed class Loader : CachedAudioAssetLoader
        {
            public readonly Dictionary<string, Action<AudioClip>> Pending = new Dictionary<string, Action<AudioClip>>();
            public readonly List<string> Released = new List<string>();
            protected override void BeginLoad(string address, Action<AudioClip> complete) { Pending[address] = complete; }
            protected override void EndLoad(string address) { Released.Add(address); }
        }
        private readonly List<Object> assets = new List<Object>();
        private readonly List<Scene> scenes = new List<Scene>();
        private IAudioAssetLoader previous;
        private Loader loader;
#if UNITY_EDITOR
        private string importedPath;
#endif
        [SetUp] public void SetUp()
        {
            AudioListener.pause = false;
            AudioSystem.Shutdown();
            previous = AudioSystem.AssetLoader;
            loader = new Loader();
            AudioSystem.AssetLoader = loader;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            AudioListener.pause = false;
            AudioSystem.Shutdown();
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            foreach (Scene scene in scenes) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
#if UNITY_EDITOR
            if (importedPath != null) UnityEditor.AssetDatabase.DeleteAsset(importedPath);
            importedPath = null;
#endif
            AudioSystem.AssetLoader = previous;
            Assert.AreEqual(0, loader.RetainedAddressCount);
        }
        private AudioClip Clip(float seconds = 1)
        {
            AudioClip clip = AudioTestUtil.MakeSine(seconds); assets.Add(clip); return clip;
        }
        private SoundEvent Event(string address, out SoundBank bank, float seconds = 1)
        {
            SoundEvent item = SoundEvent.CreateRuntime(); assets.Add(item);
            item.ClipReferences = new[] { ClipReference.Addressed(address) }; item.Loop = true; item.SpatialBlend = 0;
            bank = SoundBank.CreateRuntime(item); assets.Add(bank); AudioSystem.LoadBank(bank);
            loader.Pending[address](Clip(seconds)); return item;
        }
        private AudioBankScope Scope(Scene scene, SoundBank bank)
        {
            GameObject go = new GameObject("Scene audio ownership"); go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            AudioBankScope scope = go.AddComponent<AudioBankScope>(); scope.Banks = new[] { bank, bank };
            go.SetActive(true); return scope;
        }
        [UnityTest] public IEnumerator BankUnloadKeepsActiveSoundUntilTheLastHandleStops()
        {
            SoundBank bank; SoundEvent item = Event("sound", out bank);
            SoundHandle a = AudioSystem.PlayTracked(item); SoundHandle b = AudioSystem.PlayTracked(item);
            AudioSystem.UnloadBank(bank);
            Assert.IsFalse(item.ClipReferences[0].IsResolved); Assert.IsTrue(a.IsPlaying);
            Assert.AreEqual(1, loader.RetainedAddressCount);
            a.Stop(); Assert.IsEmpty(loader.Released);
            b.Stop(); CollectionAssert.AreEqual(new[] { "sound" }, loader.Released);
            yield return null;
        }
        [UnityTest] public IEnumerator NaturalCompletionReleasesAnUnloadedBank()
        {
            SoundBank bank; SoundEvent item = Event("natural", out bank, 0.15f); item.Loop = false;
            SoundHandle handle = AudioSystem.PlayTracked(item); AudioSystem.UnloadBank(bank);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "natural lease completion");
            CollectionAssert.AreEqual(new[] { "natural" }, loader.Released);
        }
        [UnityTest] public IEnumerator VirtualSoundRetainsItsAssetUntilStopped()
        {
            foreach (AudioListener old in Object.FindObjectsOfType<AudioListener>()) old.enabled = false;
            GameObject listener = new GameObject("Lifetime listener"); assets.Add(listener); listener.AddComponent<AudioListener>();
            SoundBank bank; SoundEvent item = Event("virtual", out bank); item.SpatialBlend = 1; item.AllowVirtualization = true;
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(item, new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "virtualized lease");
            AudioSystem.UnloadBank(bank); Assert.IsEmpty(loader.Released);
            handle.Stop(); CollectionAssert.AreEqual(new[] { "virtual" }, loader.Released);
        }
        [UnityTest] public IEnumerator MusicFadeTailRetainsTheOutgoingAsset()
        {
            SoundBank bank; SoundEvent item = Event("tail", out bank);
            MusicTrack a = MusicTrack.CreateRuntime(null, item.GetClip(0), 120, 4); assets.Add(a);
            MusicTrack b = MusicTrack.CreateRuntime(null, Clip(), 120, 4); assets.Add(b);
            AudioSystem.PlayMusic(a); AudioSystem.TransitionMusic(b, MusicQuantization.Immediate, 0.2f);
            AudioSystem.UnloadBank(bank);
            yield return AudioTestUtil.WaitUntil(() => AudioSystem.Music.CurrentTrack == b, "music boundary");
            Assert.IsTrue(AudioSystem.Music.IsCrossfading); Assert.IsEmpty(loader.Released);
            yield return AudioTestUtil.WaitUntil(() => !AudioSystem.Music.IsCrossfading, "music tail release");
            CollectionAssert.AreEqual(new[] { "tail" }, loader.Released);
        }
        [UnityTest] public IEnumerator CancellingPendingMusicReleasesOnlyTheIncomingAsset()
        {
            SoundBank bank; SoundEvent item = Event("pending", out bank);
            MusicTrack a = MusicTrack.CreateRuntime(null, Clip(), 60, 4); assets.Add(a);
            MusicTrack b = MusicTrack.CreateRuntime(null, item.GetClip(0), 60, 4); assets.Add(b);
            AudioSystem.PlayMusic(a); AudioSystem.TransitionMusic(b, MusicQuantization.Bar, 0.2f);
            AudioSystem.UnloadBank(bank); Assert.IsEmpty(loader.Released);
            Assert.IsTrue(AudioSystem.Music.CancelTransition());
            CollectionAssert.AreEqual(new[] { "pending" }, loader.Released); Assert.AreSame(a, AudioSystem.Music.CurrentTrack);
            yield return null;
        }
        [UnityTest] public IEnumerator StingerWithoutMusicReleasesAfterItsScheduledEnd()
        {
            SoundBank bank; SoundEvent item = Event("stinger", out bank, 0.15f);
            AudioSystem.PlayStinger(item.GetClip(0), MusicQuantization.Immediate); AudioSystem.UnloadBank(bank);
            Assert.IsEmpty(loader.Released);
            yield return AudioTestUtil.WaitUntil(() => loader.Released.Count == 1, "standalone stinger release");
        }
        [UnityTest] public IEnumerator QueuedSpeechRetainsItsAssetBeforeItStarts()
        {
            SoundBank bank; SoundEvent item = Event("queued", out bank, 0.15f);
            VoiceLine first = VoiceLine.CreateRuntime(Clip(0.15f), "First", "One"); assets.Add(first);
            VoiceLine second = VoiceLine.CreateRuntime(item.GetClip(0), "Second", "Two"); assets.Add(second);
            AudioSystem.PlayVoiceLine(first); AudioSystem.PlayVoiceLine(second); AudioSystem.UnloadBank(bank);
            Assert.IsEmpty(loader.Released);
            yield return AudioTestUtil.WaitUntil(() => AudioSystem.VoiceOver.CurrentLine == second, "queued speech start");
            Assert.IsEmpty(loader.Released);
            yield return AudioTestUtil.WaitUntil(() => !AudioSystem.VoiceOver.IsSpeaking, "queued speech completion");
            CollectionAssert.AreEqual(new[] { "queued" }, loader.Released);
        }
#if UNITY_EDITOR
        private AudioClip ImportedClip()
        {
            importedPath = "Assets/AudioMWLifetime_" + Guid.NewGuid().ToString("N") + ".wav";
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(System.IO.File.Create(importedPath)))
            {
                int dataSize = 44100 * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(dataSize); writer.Write(new byte[dataSize]);
            }
            UnityEditor.AssetDatabase.ImportAsset(importedPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(importedPath);
        }
        [UnityTest] public IEnumerator PlaybackStartedBeforeBankLoadStillRetainsTheClip()
        {
            AudioClip clip = ImportedClip(); SoundEvent item = SoundEvent.CreateRuntime(clip); assets.Add(item); item.Loop = true;
            SoundHandle handle = AudioSystem.PlayTracked(item);
            SoundBank bank = SoundBank.CreateRuntime(item); assets.Add(bank); AudioSystem.LoadBank(bank);
            AudioSystem.UnloadBank(bank);
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState); Assert.IsTrue(handle.IsPlaying);
            handle.Stop(); Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
            yield return null;
        }
        [UnityTest] public IEnumerator NativeBankUnloadsDataOnlyAfterPlaybackStops()
        {
            AudioClip clip = ImportedClip(); SoundEvent item = SoundEvent.CreateRuntime(clip); assets.Add(item); item.Loop = true;
            SoundBank bank = SoundBank.CreateRuntime(item); assets.Add(bank); AudioSystem.LoadBank(bank);
            SoundHandle handle = AudioSystem.PlayTracked(item); AudioSystem.UnloadBank(bank);
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState); Assert.IsTrue(handle.IsPlaying);
            handle.Stop();
            Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
            yield return null;
        }
#endif
        [UnityTest] public IEnumerator DirectBankRetainsAnAssetOriginallyResolvedByALoader()
        {
            SoundBank streamed; SoundEvent addressed = Event("mixed", out streamed);
            SoundEvent direct = SoundEvent.CreateRuntime(addressed.GetClip(0)); assets.Add(direct);
            SoundBank native = SoundBank.CreateRuntime(direct); assets.Add(native); AudioSystem.LoadBank(native);
            AudioSystem.UnloadBank(streamed); Assert.IsEmpty(loader.Released);
            AudioSystem.UnloadBank(native); CollectionAssert.AreEqual(new[] { "mixed" }, loader.Released);
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyedMusicTrackReleasesItsPlaybackHold()
        {
            SoundBank bank; SoundEvent item = Event("destroyed-music", out bank);
            MusicTrack track = MusicTrack.CreateRuntime(null, item.GetClip(0), 120, 4); assets.Add(track);
            AudioSystem.PlayMusic(track); AudioSystem.UnloadBank(bank); Object.Destroy(track);
            yield return AudioTestUtil.WaitUntil(() => loader.Released.Count == 1, "destroyed music cleanup");
            Assert.IsFalse(AudioSystem.Music.IsPlaying);
        }
        [UnityTest] public IEnumerator DestroyedPendingTrackCancelsItsHoldAndKeepsCurrentMusic()
        {
            SoundBank bank; SoundEvent item = Event("destroyed-pending", out bank);
            MusicTrack current = MusicTrack.CreateRuntime(null, Clip(), 60, 4); assets.Add(current);
            MusicTrack next = MusicTrack.CreateRuntime(null, item.GetClip(0), 60, 4); assets.Add(next);
            AudioSystem.PlayMusic(current); AudioSystem.TransitionMusic(next, MusicQuantization.Bar, 0.2f);
            AudioSystem.UnloadBank(bank); Object.Destroy(next);
            yield return AudioTestUtil.WaitUntil(() => loader.Released.Count == 1, "destroyed pending cleanup");
            Assert.AreSame(current, AudioSystem.Music.CurrentTrack); Assert.IsTrue(AudioSystem.Music.IsPlaying);
        }
        [UnityTest] public IEnumerator DestroyedVoiceLineReleasesItsPlaybackHold()
        {
            SoundBank bank; SoundEvent item = Event("destroyed-voice", out bank);
            VoiceLine line = VoiceLine.CreateRuntime(item.GetClip(0), "Destroyed", "Line"); assets.Add(line);
            AudioSystem.PlayVoiceLine(line); AudioSystem.UnloadBank(bank); Object.Destroy(line);
            yield return AudioTestUtil.WaitUntil(() => loader.Released.Count == 1, "destroyed speech cleanup");
            Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);
        }
        [UnityTest] public IEnumerator StoppingSpeechReleasesQueuedClipsWithoutStartingThem()
        {
            SoundBank bank; SoundEvent item = Event("cancelled-queue", out bank);
            VoiceLine first = VoiceLine.CreateRuntime(Clip(), "First", "Line"); assets.Add(first);
            VoiceLine queued = VoiceLine.CreateRuntime(item.GetClip(0), "Queued", "Line"); assets.Add(queued);
            AudioSystem.PlayVoiceLine(first); AudioSystem.PlayVoiceLine(queued); AudioSystem.UnloadBank(bank);
            AudioSystem.StopVoiceOver();
            Assert.AreEqual(0, AudioSystem.VoiceOver.QueueLength); Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);
            CollectionAssert.AreEqual(new[] { "cancelled-queue" }, loader.Released);
            yield return null;
        }
        [UnityTest] public IEnumerator ShortCompositeLayerReleasesBeforeALongerSibling()
        {
            SoundBank bank; SoundEvent shortLayer = Event("short-layer", out bank, 0.1f); shortLayer.Loop = false;
            SoundEvent longLayer = SoundEvent.CreateRuntime(Clip(1)); assets.Add(longLayer); longLayer.Loop = true;
            SoundEvent root = SoundEvent.CreateRuntime(); assets.Add(root); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(shortLayer), SoundEventChild.Create(longLayer) };
            SoundHandle handle = AudioSystem.PlayTracked(root); AudioSystem.UnloadBank(bank);
            yield return AudioTestUtil.WaitUntil(() => loader.Released.Count == 1, "short layer completion");
            Assert.IsTrue(handle.IsPlaying); handle.Stop();
        }
        [UnityTest] public IEnumerator AdditiveScenesKeepSharedBankUntilTheLastSceneLeaves()
        {
            SoundEvent item = SoundEvent.CreateRuntime(Clip()); assets.Add(item);
            SoundBank bank = SoundBank.CreateRuntime(item); assets.Add(bank);
            Scene a = SceneManager.CreateScene("Audio scope A"); scenes.Add(a);
            Scene b = SceneManager.CreateScene("Audio scope B"); scenes.Add(b);
            Scope(a, bank); AudioBankScope remaining = Scope(b, bank);
            Assert.AreEqual(2, bank.ScopeCount); Assert.IsTrue(remaining.IsReady);
            yield return SceneManager.UnloadSceneAsync(a);
            Assert.IsTrue(bank.IsLoaded); Assert.AreEqual(1, bank.ScopeCount); Assert.IsTrue(remaining.IsReady);
            yield return SceneManager.UnloadSceneAsync(b);
            Assert.IsFalse(bank.IsLoaded); Assert.AreEqual(0, bank.ScopeCount);
        }
        [UnityTest] public IEnumerator SceneUnloadLetsDetachedSoundFinish()
        {
            SoundBank bank; SoundEvent item = Event("scene-tail", out bank, 0.2f); item.Loop = false;
            Scene scene = SceneManager.CreateScene("Audio tail scene"); scenes.Add(scene); Scope(scene, bank);
            AudioSystem.UnloadBank(bank);
            SoundHandle handle = AudioSystem.PlayTracked(item);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.IsFalse(bank.IsLoaded); Assert.IsEmpty(loader.Released); Assert.IsTrue(handle.IsPlaying);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "detached scene tail");
            CollectionAssert.AreEqual(new[] { "scene-tail" }, loader.Released);
        }
    }
}
