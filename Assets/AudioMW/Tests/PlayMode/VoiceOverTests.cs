using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class VoiceOverTests
    {
        [SetUp]
        public void SetUp()
        {
            AudioSystem.StopVoiceOver();
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopVoiceOver();
            AudioSystem.VoiceOver.DuckParameter = null;
        }

        [UnityTest]
        public IEnumerator PlayStartsLineImmediately()
        {
            VoiceLine line = MakeLine("Anna", "Hello there", 0.4f);

            Assert.IsTrue(AudioSystem.PlayVoiceLine(line));
            Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);
            Assert.AreSame(line, AudioSystem.VoiceOver.CurrentLine);

            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidLineIsRejected()
        {
            Assert.IsFalse(AudioSystem.PlayVoiceLine(null));
            Assert.IsFalse(AudioSystem.PlayVoiceLine(VoiceLine.CreateRuntime(null, "nobody", "silence")));
            Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SecondLineQueuesByDefault()
        {
            AudioSystem.PlayVoiceLine(MakeLine("Anna", "First", 0.3f));
            AudioSystem.PlayVoiceLine(MakeLine("Boris", "Second", 0.3f));

            Assert.AreEqual(1, AudioSystem.VoiceOver.QueueLength);

            yield return null;
        }

        [UnityTest]
        public IEnumerator IgnoreIfBusyDropsLine()
        {
            AudioSystem.PlayVoiceLine(MakeLine("Anna", "First", 0.5f));

            bool accepted = AudioSystem.PlayVoiceLine(MakeLine("Boris", "Second", 0.3f), VoiceOverMode.IgnoreIfBusy);

            Assert.IsFalse(accepted);
            Assert.AreEqual(0, AudioSystem.VoiceOver.QueueLength);

            yield return null;
        }

        [UnityTest]
        public IEnumerator InterruptReplacesCurrentLine()
        {
            VoiceLine first = MakeLine("Anna", "First", 0.5f);
            VoiceLine second = MakeLine("Boris", "Second", 0.3f);

            AudioSystem.PlayVoiceLine(first);
            AudioSystem.PlayVoiceLine(second, VoiceOverMode.Interrupt);

            Assert.AreSame(second, AudioSystem.VoiceOver.CurrentLine);
            Assert.AreEqual(0, AudioSystem.VoiceOver.QueueLength);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LowerPriorityCannotInterrupt()
        {
            VoiceLine important = MakeLine("Anna", "Critical", 0.5f);
            important.Priority = 10;

            VoiceLine chatter = MakeLine("Boris", "Barks", 0.3f);
            chatter.Priority = 1;

            AudioSystem.PlayVoiceLine(important);
            bool accepted = AudioSystem.PlayVoiceLine(chatter, VoiceOverMode.Interrupt);

            Assert.IsFalse(accepted);
            Assert.AreSame(important, AudioSystem.VoiceOver.CurrentLine);

            yield return null;
        }

        [UnityTest]
        public IEnumerator QueueAdvancesAfterLineEnds()
        {
            VoiceLine first = MakeLine("Anna", "First", 0.15f);
            VoiceLine second = MakeLine("Boris", "Second", 1f);
            first.TrailingSilence = 0f;

            AudioSystem.PlayVoiceLine(first);
            AudioSystem.PlayVoiceLine(second);

            yield return new WaitForSeconds(0.45f);

            Assert.AreSame(second, AudioSystem.VoiceOver.CurrentLine);
            Assert.AreEqual(0, AudioSystem.VoiceOver.QueueLength);
        }

        [UnityTest]
        public IEnumerator SubtitleEventsFireOnStartAndEnd()
        {
            List<string> subtitles = new List<string>();
            AudioSystem.VoiceOver.SubtitleChanged += text => subtitles.Add(text);

            VoiceLine line = MakeLine("Anna", "Watch out", 0.15f);
            line.TrailingSilence = 0f;

            AudioSystem.PlayVoiceLine(line);

            yield return new WaitForSeconds(0.5f);

            Assert.Contains("Watch out", subtitles);
            Assert.AreEqual(string.Empty, subtitles[subtitles.Count - 1]);
        }

        [UnityTest]
        public IEnumerator DuckParameterRisesWhileSpeaking()
        {
            SoundParameter duck = SoundParameter.CreateRuntime(0f, 1f, 0f);
            AudioSystem.VoiceOver.DuckParameter = duck;
            AudioSystem.VoiceOver.DuckFadeSeconds = 0.05f;

            AudioSystem.PlayVoiceLine(MakeLine("Anna", "Ducking", 0.6f));

            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(1f, AudioSystem.GetParameter(duck), 0.05f);
        }

        [UnityTest]
        public IEnumerator DuckParameterFallsAfterSpeech()
        {
            SoundParameter duck = SoundParameter.CreateRuntime(0f, 1f, 0f);
            AudioSystem.VoiceOver.DuckParameter = duck;
            AudioSystem.VoiceOver.DuckFadeSeconds = 0.05f;

            VoiceLine line = MakeLine("Anna", "Short", 0.15f);
            line.TrailingSilence = 0f;

            AudioSystem.PlayVoiceLine(line);

            yield return new WaitForSeconds(0.7f);

            Assert.AreEqual(0f, AudioSystem.GetParameter(duck), 0.05f);
        }

        [UnityTest]
        public IEnumerator StopClearsQueueAndCurrentLine()
        {
            AudioSystem.PlayVoiceLine(MakeLine("Anna", "First", 0.5f));
            AudioSystem.PlayVoiceLine(MakeLine("Boris", "Second", 0.5f));

            AudioSystem.StopVoiceOver();

            Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);
            Assert.AreEqual(0, AudioSystem.VoiceOver.QueueLength);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipEndsCurrentLineOnly()
        {
            AudioSystem.PlayVoiceLine(MakeLine("Anna", "First", 0.5f));
            AudioSystem.PlayVoiceLine(MakeLine("Boris", "Second", 0.5f));

            AudioSystem.SkipVoiceLine();

            Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);
            Assert.AreEqual(1, AudioSystem.VoiceOver.QueueLength);

            yield return null;
        }

        private static VoiceLine MakeLine(string speaker, string subtitle, float seconds)
        {
            VoiceLine line = VoiceLine.CreateRuntime(MakeSine(seconds), speaker, subtitle);
            line.Volume = 0.05f;
            return line;
        }

        private static AudioClip MakeSine(float seconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * seconds));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * 180f * i / sampleRate) * 0.2f;
            }

            AudioClip clip = AudioClip.Create("AudioMW_VoiceSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
