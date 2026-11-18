using System;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public class StudioMonitoringTests
    {
        [Test]
        public void ReadoutUsesAbsoluteFramesAtTheChosenReferenceRate()
        {
            Assert.AreEqual("00:00.000   F 0", AudioEventEditorWindow.FormatClockAndFrame(0,1));
            Assert.AreEqual("00:01.000   F 25", AudioEventEditorWindow.FormatClockAndFrame(1,2));
            Assert.AreEqual("16:41.000   F 30000", AudioEventEditorWindow.FormatClockAndFrame(1001,3));
            Assert.AreEqual("00:00.000   F 0", AudioEventEditorWindow.FormatClockAndFrame(-2,7));
        }

        [Test]
        public void ContiguousBlocksMatchWholeClipAndKeepStereoLevelsSeparate()
        {
            const int rate = 24000, frames = 4 * rate;
            var samples = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                float tone = .2f * Mathf.Sin((i+.5f) * Mathf.PI * .5f);
                samples[i*2] = tone; samples[i*2+1] = tone*.5f;
            }
            var reference = LoudnessMeter.Analyze(samples,2,rate);
            var meter = new PcmLevelMeter(2,rate);
            var part = new float[2*377];
            for (int i=0; i<frames; i+=377)
            {
                int size = Math.Min(377,frames-i);
                Array.Copy(samples,i*2,part,0,size*2);
                meter.Push(part,size);
            }
            Assert.AreEqual(reference.IntegratedLufs,meter.Integrated(),.001);
            Assert.AreEqual(reference.ShortTermMaxLufs,meter.ShortTerm,.01);
            Assert.AreEqual(6.0206,meter.Peak[0]-meter.Peak[1],.002);
            Assert.Greater(meter.MaxTruePeak[0],meter.Peak[0]+1);
            Assert.AreEqual(reference.TruePeakDb,meter.MaxTruePeak[0],.15);
        }

        [Test]
        public void RepeatedDryMeterReadsStartAtTheAuthoredTrim()
        {
            var source = AudioClip.Create("trim", 48000, 1, 48000, false);
            try
            {
                var clip = new ArrangementClip { Source = source, Start = 3, Trim = .25, Repeat = true };
                Assert.AreEqual(.375, AudioEventEditorWindow.DrySourcePosition(clip, 3.875), 1e-8);
                Assert.AreEqual(.25, AudioEventEditorWindow.DrySourcePosition(clip, 4.5), 1e-8);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void MeterDistinguishesWarmupFromSilence()
        {
            var meter = new PcmLevelMeter(1,48000);
            meter.Push(new float[4800],4800);
            Assert.IsTrue(double.IsNaN(meter.Momentary));
            Assert.IsTrue(double.IsNegativeInfinity(meter.Peak[0]));
            meter.Push(new float[14400],14400);
            Assert.IsTrue(double.IsNegativeInfinity(meter.Momentary));
            Assert.IsTrue(double.IsNegativeInfinity(meter.Integrated()));
            Assert.IsTrue(double.IsNaN(meter.ShortTerm));
        }
    }
}
