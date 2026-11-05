using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class BankLifetimeTests
    {
        private sealed class Loader : IAudioAssetLoader
        {
            public readonly List<Action<string, AudioClip>> Pending = new List<Action<string, AudioClip>>();
            public int Releases;
            public bool CanLoad(string address) { return true; }
            public void LoadAsync(string address, Action<string, AudioClip> callback) { Pending.Add(callback); }
            public void Release(string address) { Releases++; }
            public void ReleaseAll() { }
        }
        private readonly List<Object> assets = new List<Object>();
        [TearDown] public void TearDown() { foreach (Object item in assets) if (item != null) Object.DestroyImmediate(item); assets.Clear(); }
        private SoundBank Bank(out ClipReference reference)
        {
            SoundBank bank = SoundBank.CreateRuntime(); assets.Add(bank);
            reference = ClipReference.Addressed("clip"); bank.StreamedClips = new[] { reference }; return bank;
        }
        private AudioClip Clip() { AudioClip clip = AudioClip.Create("Bank lifetime", 4410, 1, 44100, false); assets.Add(clip); return clip; }

        [Test] public void RepeatedPendingLoadDoesNotAcquireTwice()
        {
            ClipReference reference; SoundBank bank = Bank(out reference); Loader loader = new Loader();
            bank.LoadStreamed(loader); bank.LoadStreamed(loader);
            Assert.AreEqual(1, loader.Pending.Count); Assert.AreEqual(1, bank.PendingStreamCount);
        }
        [Test] public void CompletionAfterUnloadCannotResolveTheReference()
        {
            ClipReference reference; SoundBank bank = Bank(out reference); Loader loader = new Loader();
            bank.LoadStreamed(loader); bank.UnloadStreamed(loader); loader.Pending[0]("clip", Clip());
            Assert.IsFalse(reference.IsResolved); Assert.AreEqual(0, bank.PendingStreamCount);
        }
        [Test] public void StaleCompletionCannotCompleteANewerRequest()
        {
            ClipReference reference; SoundBank bank = Bank(out reference); Loader loader = new Loader();
            bank.LoadStreamed(loader); bank.UnloadStreamed(loader); bank.LoadStreamed(loader);
            loader.Pending[0]("clip", Clip()); Assert.AreEqual(1, bank.PendingStreamCount); Assert.IsFalse(reference.IsResolved);
            AudioClip current = Clip(); loader.Pending[1]("clip", current);
            Assert.AreSame(current, reference.Clip); Assert.AreEqual(0, bank.PendingStreamCount);
        }
        [Test] public void UnloadUsesTheOriginalLoaderAfterFacadeChanges()
        {
            ClipReference reference; SoundBank bank = Bank(out reference); Loader original = new Loader(); Loader replacement = new Loader();
            bank.LoadStreamed(original); bank.UnloadStreamed(replacement);
            Assert.AreEqual(1, original.Releases); Assert.AreEqual(0, replacement.Releases);
        }
        [Test] public void DestroyingBankReleasesItsPendingRequest()
        {
            ClipReference reference; SoundBank bank = Bank(out reference); Loader loader = new Loader();
            bank.LoadStreamed(loader); Object.DestroyImmediate(bank); Assert.AreEqual(1, loader.Releases);
            Assert.DoesNotThrow(() => loader.Pending[0]("clip", Clip())); Assert.IsFalse(reference.IsResolved);
        }
        [Test] public void CollectionFindsNestedChildrenWithoutFollowingCyclesForever()
        {
            AudioClip clip = Clip(); SoundEvent leaf = SoundEvent.CreateRuntime(clip); assets.Add(leaf);
            SoundEvent root = SoundEvent.CreateRuntime(); assets.Add(root); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(root), SoundEventChild.Create(leaf) };
            List<AudioClip> clips = new List<AudioClip>(); SoundBank.CollectClips(root, clips);
            CollectionAssert.AreEqual(new[] { clip }, clips);
        }
    }
}
