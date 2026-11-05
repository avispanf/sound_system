using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class BankScopeTests
    {
        private sealed class Loader : CachedAudioAssetLoader
        {
            public Action<AudioClip> Complete;
            public int Releases;
            protected override void BeginLoad(string address, Action<AudioClip> complete) { Complete = complete; }
            protected override void EndLoad(string address) { Releases++; }
        }
        private readonly List<Object> assets = new List<Object>();
        [TearDown] public void TearDown()
        {
            SoundBank.UnloadAll();
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }
        private SoundBank Bank()
        {
            SoundBank bank = SoundBank.CreateRuntime(); assets.Add(bank);
            bank.StreamedClips = new[] { ClipReference.Addressed("scope") }; return bank;
        }
        [Test] public void IndependentScopesAcquireOnceAndReleaseAfterTheLastOwner()
        {
            Loader loader = new Loader(); SoundBank bank = Bank();
            IDisposable a = bank.Acquire(loader); IDisposable b = bank.Acquire(loader);
            Assert.AreEqual(1, bank.PendingStreamCount); Assert.AreEqual(2, bank.ScopeCount);
            a.Dispose(); a.Dispose(); Assert.AreEqual(1, bank.ScopeCount); Assert.AreEqual(0, loader.Releases);
            b.Dispose(); Assert.AreEqual(0, bank.PendingStreamCount); Assert.AreEqual(1, loader.Releases); Assert.IsFalse(bank.IsLoaded);
            AudioClip late = AudioClip.Create("Late", 4410, 1, 44100, false); assets.Add(late); loader.Complete(late);
            Assert.IsFalse(bank.StreamedClips[0].IsResolved);
        }
        [Test] public void ManualOwnershipAndScopeOwnershipAreIndependent()
        {
            Loader loader = new Loader(); SoundBank bank = Bank(); bank.Load(); bank.LoadStreamed(loader);
            IDisposable scope = bank.Acquire(loader); bank.Unload(); bank.UnloadStreamed(loader);
            Assert.IsTrue(bank.IsLoaded); Assert.AreEqual(0, loader.Releases);
            scope.Dispose(); Assert.IsFalse(bank.IsLoaded); Assert.AreEqual(1, loader.Releases);
            scope = bank.Acquire(loader); bank.Load(); bank.LoadStreamed(loader); scope.Dispose();
            Assert.IsTrue(bank.IsLoaded); Assert.AreEqual(1, loader.Releases);
            bank.UnloadStreamed(loader); bank.Unload(); Assert.AreEqual(2, loader.Releases);
        }
        [Test] public void ShutdownInvalidatesOldScopesWithoutAffectingNewOwners()
        {
            Loader loader = new Loader(); SoundBank bank = Bank(); IDisposable old = bank.Acquire(loader);
            SoundBank.UnloadAll(); IDisposable current = bank.Acquire(loader); old.Dispose();
            Assert.AreEqual(1, bank.ScopeCount); Assert.IsTrue(bank.IsLoaded); Assert.AreEqual(1, loader.Releases);
            current.Dispose(); Assert.AreEqual(2, loader.Releases);
        }
        [Test] public void ConflictingLoaderCannotReplaceAnotherScopesResources()
        {
            Loader original = new Loader(); Loader other = new Loader(); SoundBank bank = Bank();
            IDisposable owner = bank.Acquire(original);
            Assert.Throws<InvalidOperationException>(() => bank.Acquire(other));
            Assert.Throws<InvalidOperationException>(() => bank.LoadStreamed(other));
            Assert.AreEqual(1, bank.ScopeCount); Assert.AreEqual(0, original.Releases); Assert.AreEqual(0, other.RetainedAddressCount);
            owner.Dispose(); Assert.AreEqual(1, original.Releases);
        }
    }
}
