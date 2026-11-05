using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class CachedLoaderTests
    {
        private sealed class Loader : CachedAudioAssetLoader
        {
            public readonly List<Action<AudioClip>> Pending = new List<Action<AudioClip>>();
            public int Ends;
            public bool Throw;
            protected override void BeginLoad(string address, Action<AudioClip> callback)
            {
                if (Throw) throw new InvalidOperationException("Provider failed");
                Pending.Add(callback);
            }
            protected override void EndLoad(string address) { Ends++; }
        }
        private readonly List<Object> assets = new List<Object>();
        [TearDown] public void TearDown() { foreach (Object item in assets) if (item != null) Object.DestroyImmediate(item); assets.Clear(); }
        private AudioClip Clip() { AudioClip clip = AudioClip.Create("Shared loader", 1024, 1, 44100, false); assets.Add(clip); return clip; }
        private SoundBank Bank(ClipReference reference) { SoundBank bank = SoundBank.CreateRuntime(); bank.StreamedClips = new[] { reference }; assets.Add(bank); return bank; }

        [Test] public void ConcurrentConsumersShareOneLoadAndReleaseAfterLastOwner()
        {
            Loader loader = new Loader(); int callbacks = 0;
            loader.LoadAsync("a", (_, clip) => callbacks++); loader.LoadAsync("a", (_, clip) => callbacks++);
            Assert.AreEqual(1, loader.Pending.Count);
            loader.Release("a"); Assert.AreEqual(0, loader.Ends);
            loader.Pending[0](Clip()); Assert.AreEqual(2, callbacks);
            loader.Release("a"); Assert.AreEqual(1, loader.Ends); Assert.AreEqual(0, loader.RetainedAddressCount);
        }
        [Test] public void LateCompletionCannotPopulateANewGeneration()
        {
            Loader loader = new Loader(); int old = 0, current = 0;
            loader.LoadAsync("a", (_, clip) => old++); loader.Release("a");
            loader.LoadAsync("a", (_, clip) => current++);
            loader.Pending[0](Clip()); Assert.AreEqual(0, old); Assert.AreEqual(0, current);
            loader.Pending[1](Clip()); Assert.AreEqual(1, current); loader.ReleaseAll();
        }
        [Test] public void CompletedCacheAddsOwnershipWithoutAnotherBackendLoad()
        {
            Loader loader = new Loader(); loader.LoadAsync("a", null); AudioClip clip = Clip(); loader.Pending[0](clip);
            AudioClip result = null; loader.LoadAsync("a", (_, value) => result = value);
            Assert.AreSame(clip, result); Assert.AreEqual(1, loader.Pending.Count);
            loader.Release("a"); Assert.AreEqual(0, loader.Ends); loader.Release("a"); Assert.AreEqual(1, loader.Ends);
        }
        [Test] public void ReleaseAllInvalidatesOutstandingCallbacks()
        {
            Loader loader = new Loader(); int called = 0;
            loader.LoadAsync("a", (_, clip) => called++); loader.LoadAsync("b", (_, clip) => called++);
            loader.ReleaseAll(); foreach (Action<AudioClip> complete in loader.Pending) complete(Clip());
            Assert.AreEqual(0, called); Assert.AreEqual(2, loader.Ends);
        }
        [Test] public void SharedReferenceKeepsOtherBanksOwnershipWhenOneUnloads()
        {
            Loader loader = new Loader(); ClipReference shared = ClipReference.Addressed("a");
            SoundBank a = Bank(shared), b = Bank(shared);
            a.LoadStreamed(loader); b.LoadStreamed(loader); AudioClip clip = Clip(); loader.Pending[0](clip);
            a.UnloadStreamed(null); Assert.AreSame(clip, shared.Clip); Assert.AreEqual(0, loader.Ends);
            b.UnloadStreamed(null); Assert.IsFalse(shared.IsResolved); Assert.AreEqual(1, loader.Ends);
        }
        [Test] public void FailedBankLoadCanBeRetriedWithoutLeakingOwnership()
        {
            Loader loader = new Loader(); SoundBank bank = Bank(ClipReference.Addressed("a"));
            bank.LoadStreamed(loader); loader.Pending[0](null); Assert.AreEqual(0, bank.PendingStreamCount); Assert.AreEqual(1, loader.Ends);
            bank.LoadStreamed(loader); Assert.AreEqual(2, loader.Pending.Count); loader.Pending[1](Clip()); Assert.AreEqual(0, bank.PendingStreamCount);
        }
        [Test] public void ThrowingProviderDoesNotLeaveBankPending()
        {
            Loader loader = new Loader { Throw = true }; SoundBank bank = Bank(ClipReference.Addressed("a"));
            Assert.Throws<InvalidOperationException>(() => bank.LoadStreamed(loader));
            Assert.AreEqual(0, bank.PendingStreamCount); Assert.AreEqual(0, loader.RetainedAddressCount);
        }
        [Test] public void ACallbackExceptionDoesNotBlockOtherConsumers()
        {
            Loader loader = new Loader(); bool called = false;
            loader.LoadAsync("a", (_, clip) => { throw new InvalidOperationException("test callback"); });
            loader.LoadAsync("a", (_, clip) => called = true);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: test callback"));
            loader.Pending[0](Clip()); Assert.IsTrue(called); loader.ReleaseAll();
        }
        [Test] public void DuplicateBankAddressesAcquireOnceAndResolveEveryRow()
        {
            Loader loader = new Loader(); ClipReference a = ClipReference.Addressed("a"), b = ClipReference.Addressed("a");
            SoundBank bank = Bank(a); bank.StreamedClips = new[] { a, b };
            bank.LoadStreamed(loader); Assert.AreEqual(1, loader.Pending.Count); AudioClip clip = Clip(); loader.Pending[0](clip);
            Assert.AreSame(clip, a.Clip); Assert.AreSame(clip, b.Clip); bank.UnloadStreamed(null); Assert.AreEqual(1, loader.Ends);
        }

        [Test] public void BankDiscoversEventAddressesAndResolvedClipCanBeSelected()
        {
            Loader loader = new Loader(); ClipReference reference = ClipReference.Addressed("event-clip");
            SoundEvent item = SoundEvent.CreateRuntime(); assets.Add(item); item.ClipReferences = new[] { reference };
            SoundBank bank = SoundBank.CreateRuntime(item); assets.Add(bank);
            Assert.IsFalse(item.Resolve(new System.Random(1)).IsValid);
            bank.Load(); bank.LoadStreamed(loader); Assert.AreEqual(1, bank.PendingStreamCount); Assert.IsFalse(bank.IsFullyLoaded());
            AudioClip clip = Clip(); loader.Pending[0](clip);
            Assert.AreSame(clip, item.Resolve(new System.Random(1)).Clip); Assert.IsTrue(bank.IsFullyLoaded());
            bank.UnloadStreamed(null); Assert.IsTrue(item.HasUnresolvedReferences);
        }
        [Test] public void ReplacingAuthoredReferenceArrayStillReleasesOriginalOwnership()
        {
            Loader loader = new Loader(); ClipReference reference = ClipReference.Addressed("a"); SoundBank bank = Bank(reference);
            bank.LoadStreamed(loader); loader.Pending[0](Clip()); bank.StreamedClips = new ClipReference[0];
            bank.UnloadStreamed(null); Assert.IsFalse(reference.IsResolved); Assert.AreEqual(1, loader.Ends);
        }
        [Test] public void DistinctBanksRetainSharedDirectClipUntilLastUnload()
        {
            AudioClip clip = Clip(); SoundEvent item = SoundEvent.CreateRuntime(clip); assets.Add(item);
            SoundBank a = SoundBank.CreateRuntime(item), b = SoundBank.CreateRuntime(item); assets.Add(a); assets.Add(b);
            a.Load(); b.Load(); a.Unload(); Assert.IsTrue(b.IsFullyLoaded()); b.Unload(); Assert.AreEqual(0, b.LoadedClipCount);
        }
    }
}
