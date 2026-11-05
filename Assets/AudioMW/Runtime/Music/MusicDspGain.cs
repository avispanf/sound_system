using System.Threading;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicDspGain : MonoBehaviour
    {
        private DspGainEnvelope envelope = new DspGainEnvelope();
        private int sampleRate;
        private long processedFrames;
        public long ProcessedFrames { get { return Interlocked.Read(ref processedFrames); } }
        public DspGainEnvelope Envelope { get { return Volatile.Read(ref envelope); } }
        public void SetEnvelope(DspGainEnvelope value) { Volatile.Write(ref envelope, value); }

        private void OnEnable()
        {
            sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged; }
        private void OnAudioConfigurationChanged(bool changed) { Volatile.Write(ref sampleRate, AudioSettings.outputSampleRate); }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int rate = Volatile.Read(ref sampleRate);
            if (rate <= 0 || channels <= 0) return;
            Volatile.Read(ref envelope).Process(data, channels, AudioSettings.dspTime, rate);
            Interlocked.Add(ref processedFrames, data.Length / channels);
        }
    }
}
