using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public static class AudioTestUtil
    {
        public const float DefaultTimeout = 5f;

        public static IEnumerator WaitUntil(Func<bool> condition, string description, float timeoutSeconds = DefaultTimeout)
        {
            if (condition == null)
            {
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("Timed out after " + timeoutSeconds.ToString("F1") + " s waiting for: " + description);
                    yield break;
                }

                yield return null;
            }
        }

        public static IEnumerator WaitForApproximately(Func<float> value, float expected, float tolerance, string description, float timeoutSeconds = DefaultTimeout)
        {
            yield return WaitUntil(() => Mathf.Abs(value() - expected) <= tolerance, description, timeoutSeconds);
        }

        public static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        public static AudioClip MakeSine(float seconds, float frequency = 440f, float amplitude = 0.2f, int sampleRate = 44100)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * seconds));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * amplitude;
            }

            AudioClip clip = AudioClip.Create("AudioMW_TestSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
