using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class AudioTierConfigTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioRuntimeSettings.Reset();
        }

        [Test]
        public void BufferPresetsMapToKnownSizes()
        {
            Assert.AreEqual(256, AudioTierConfig.BufferLengthFor(DspBufferPreset.BestLatency));
            Assert.AreEqual(512, AudioTierConfig.BufferLengthFor(DspBufferPreset.GoodLatency));
            Assert.AreEqual(1024, AudioTierConfig.BufferLengthFor(DspBufferPreset.BestPerformance));
        }

        [Test]
        public void MobileTierIsLeanest()
        {
            AudioTierConfig mobile = AudioTierConfig.Mobile2D();
            AudioTierConfig standard = AudioTierConfig.Standard3D();
            AudioTierConfig high = AudioTierConfig.HighEnd();

            Assert.Less(mobile.MaxVoices, standard.MaxVoices);
            Assert.Less(standard.MaxVoices, high.MaxVoices);
            Assert.Greater(mobile.BufferLength, high.BufferLength);
        }

        [Test]
        public void MobileTierDisablesSpatialFeatures()
        {
            AudioTierConfig mobile = AudioTierConfig.Mobile2D();

            Assert.IsFalse(mobile.SpatialAudioEnabled);
            Assert.IsFalse(mobile.OcclusionEnabled);
            Assert.IsFalse(mobile.CustomDspEnabled);
        }

        [Test]
        public void HighEndTierPrewarmsVoices()
        {
            AudioTierConfig high = AudioTierConfig.HighEnd();

            Assert.Greater(high.PrewarmVoices, 0);
            Assert.LessOrEqual(high.PrewarmVoices, high.MaxVoices);
        }

        [Test]
        public void RealVoiceCountNeverExceedsMaxVoices()
        {
            AudioTierConfig config = AudioTierConfig.CreateRuntime("tiny", 4, DspBufferPreset.GoodLatency);

            Assert.LessOrEqual(config.RealVoiceCount, config.MaxVoices);
        }

        [Test]
        public void MaxVoicesIsClamped()
        {
            Assert.AreEqual(1, AudioTierConfig.CreateRuntime("floor", -20, DspBufferPreset.GoodLatency).MaxVoices);
            Assert.AreEqual(512, AudioTierConfig.CreateRuntime("ceiling", 9999, DspBufferPreset.GoodLatency).MaxVoices);
        }

        [Test]
        public void SettingsClampAndReset()
        {
            AudioRuntimeSettings.MaxVoices = 9999;
            Assert.AreEqual(512, AudioRuntimeSettings.MaxVoices);

            AudioRuntimeSettings.MaxVoices = 0;
            Assert.AreEqual(1, AudioRuntimeSettings.MaxVoices);

            AudioRuntimeSettings.Reset();
            Assert.AreEqual(AudioRuntimeSettings.FallbackMaxVoices, AudioRuntimeSettings.MaxVoices);
        }

        [Test]
        public void ApplyingNullTierIsSafe()
        {
            Assert.DoesNotThrow(() => AudioTierApplier.Apply(null));
            Assert.IsFalse(AudioTierApplier.ApplyAudioSettings(null));
        }

        [Test]
        public void ApplyStoresActiveTierAndVoiceBudget()
        {
            AudioTierConfig config = AudioTierConfig.CreateRuntime("test", 12, DspBufferPreset.GoodLatency);

            AudioRuntimeSettings.MaxVoices = config.MaxVoices;
            AudioRuntimeSettings.ActiveTier = config;

            Assert.AreEqual(12, AudioRuntimeSettings.MaxVoices);
            Assert.AreSame(config, AudioRuntimeSettings.ActiveTier);
        }
    }
}
