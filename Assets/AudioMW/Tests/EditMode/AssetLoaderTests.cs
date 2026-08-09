using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class AssetLoaderTests
    {
        private sealed class FakeLoader : IAudioAssetLoader
        {
            private readonly Dictionary<string, AudioClip> library = new Dictionary<string, AudioClip>();
            private readonly List<Action> pending = new List<Action>();

            public readonly List<string> Released = new List<string>();
            public bool Deferred;

            public void Register(string address, AudioClip clip)
            {
                library[address] = clip;
            }

            public bool CanLoad(string address)
            {
                return !string.IsNullOrEmpty(address);
            }

            public void LoadAsync(string address, Action<string, AudioClip> onComplete)
            {
                AudioClip clip;
                library.TryGetValue(address, out clip);

                Action complete = () =>
                {
                    if (onComplete != null)
                    {
                        onComplete(address, clip);
                    }
                };

                if (Deferred)
                {
                    pending.Add(complete);
                }
                else
                {
                    complete();
                }
            }

            public void CompletePending()
            {
                List<Action> copy = new List<Action>(pending);
                pending.Clear();

                for (int i = 0; i < copy.Count; i++)
                {
                    copy[i]();
                }
            }

            public void Release(string address)
            {
                Released.Add(address);
            }

            public void ReleaseAll()
            {
                Released.Clear();
            }
        }

        [Test]
        public void DirectReferenceNeedsNoLoading()
        {
            AudioClip clip = MakeClip();
            ClipReference reference = ClipReference.Direct(clip);

            Assert.IsFalse(reference.IsAddressed);
            Assert.IsTrue(reference.IsResolved);
            Assert.AreSame(clip, reference.Clip);
        }

        [Test]
        public void AddressedReferenceStartsUnresolved()
        {
            ClipReference reference = ClipReference.Addressed("sfx/footstep");

            Assert.IsTrue(reference.IsAddressed);
            Assert.IsFalse(reference.IsResolved);
            Assert.IsNull(reference.Clip);
        }

        [Test]
        public void NullLoaderResolvesNothing()
        {
            ClipReference reference = ClipReference.Addressed("sfx/footstep");
            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { reference };

            bank.LoadStreamed(NullAudioAssetLoader.Instance);

            Assert.IsFalse(reference.IsResolved);
            Assert.IsTrue(bank.IsStreamingComplete);
        }

        [Test]
        public void LoaderResolvesAddressedClips()
        {
            AudioClip clip = MakeClip();
            FakeLoader loader = new FakeLoader();
            loader.Register("sfx/footstep", clip);

            ClipReference reference = ClipReference.Addressed("sfx/footstep");
            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { reference };

            bank.LoadStreamed(loader);

            Assert.IsTrue(reference.IsResolved);
            Assert.AreSame(clip, reference.Clip);
            Assert.IsTrue(bank.IsStreamingComplete);
        }

        [Test]
        public void PendingCountTracksDeferredLoads()
        {
            FakeLoader loader = new FakeLoader { Deferred = true };
            loader.Register("a", MakeClip());
            loader.Register("b", MakeClip());

            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { ClipReference.Addressed("a"), ClipReference.Addressed("b") };

            bank.LoadStreamed(loader);

            Assert.AreEqual(2, bank.PendingStreamCount);
            Assert.IsFalse(bank.IsStreamingComplete);

            loader.CompletePending();

            Assert.AreEqual(0, bank.PendingStreamCount);
            Assert.IsTrue(bank.IsStreamingComplete);
        }

        [Test]
        public void MissingAddressResolvesToNullWithoutThrowing()
        {
            FakeLoader loader = new FakeLoader();

            ClipReference reference = ClipReference.Addressed("missing");
            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { reference };

            Assert.DoesNotThrow(() => bank.LoadStreamed(loader));
            Assert.IsFalse(reference.IsResolved);
            Assert.IsTrue(bank.IsStreamingComplete);
        }

        [Test]
        public void UnloadReleasesAddressesAndClearsResolved()
        {
            AudioClip clip = MakeClip();
            FakeLoader loader = new FakeLoader();
            loader.Register("sfx/footstep", clip);

            ClipReference reference = ClipReference.Addressed("sfx/footstep");
            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { reference };

            bank.LoadStreamed(loader);
            bank.UnloadStreamed(loader);

            Assert.IsFalse(reference.IsResolved);
            Assert.Contains("sfx/footstep", loader.Released);
        }

        [Test]
        public void AlreadyResolvedReferenceIsNotReloaded()
        {
            FakeLoader loader = new FakeLoader { Deferred = true };
            loader.Register("a", MakeClip());

            ClipReference reference = ClipReference.Addressed("a");
            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { reference };

            bank.LoadStreamed(loader);
            loader.CompletePending();
            bank.LoadStreamed(loader);

            Assert.AreEqual(0, bank.PendingStreamCount);
        }

        [Test]
        public void DirectReferencesAreSkippedByStreaming()
        {
            FakeLoader loader = new FakeLoader { Deferred = true };

            SoundBank bank = SoundBank.CreateRuntime();
            bank.StreamedClips = new[] { ClipReference.Direct(MakeClip()) };

            bank.LoadStreamed(loader);

            Assert.AreEqual(0, bank.PendingStreamCount);
        }

        [Test]
        public void FacadeFallsBackToNullLoader()
        {
            AudioSystem.AssetLoader = null;

            Assert.AreSame(NullAudioAssetLoader.Instance, AudioSystem.AssetLoader);
        }

        private static AudioClip MakeClip()
        {
            AudioClip clip = AudioClip.Create("AudioMW_LoaderClip", 512, 1, 44100, false);
            clip.SetData(new float[512], 0);
            return clip;
        }
    }
}
