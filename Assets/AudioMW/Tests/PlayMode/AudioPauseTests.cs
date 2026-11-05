using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class AudioPauseTests
    {
        private readonly List<Object> assets = new List<Object>();
        [SetUp] public void SetUp() { AudioListener.pause = false; AudioSystem.StopAll(); AudioSystem.StopVoiceOver(); AudioSystem.StopMusic(); }
        [TearDown] public void TearDown()
        {
            AudioListener.pause = false; AudioSystem.Shutdown();
            foreach (Object item in assets) if (item != null) Object.Destroy(item); assets.Clear();
        }
        private AudioClip Clip(float seconds) { AudioClip clip = AudioTestUtil.MakeSine(seconds); assets.Add(clip); return clip; }
        private SoundEvent Event(float seconds, bool spatial)
        {
            SoundEvent item = SoundEvent.CreateRuntime(Clip(seconds)); assets.Add(item); item.SpatialBlend = spatial ? 1 : 0; item.AllowVirtualization = spatial; return item;
        }
        [UnityTest] public IEnumerator VirtualOneShotDoesNotExpireDuringListenerPause()
        {
            foreach (AudioListener old in Object.FindObjectsOfType<AudioListener>()) old.enabled = false;
            GameObject listener = new GameObject("Pause listener"); listener.AddComponent<AudioListener>(); assets.Add(listener);
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(Event(0.35f, true), new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "virtualization");
            AudioListener.pause = true;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(handle.IsPlaying); Assert.IsTrue(handle.IsVirtual);
            AudioListener.pause = false;
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "virtual completion after resume");
        }
        [UnityTest] public IEnumerator DuckEnvelopeAndSpeechRemainFrozenWhilePaused()
        {
            SoundParameter duck = SoundParameter.CreateRuntime(0, 1, 0); assets.Add(duck);
            VoiceLine line = VoiceLine.CreateRuntime(Clip(1), "Pause", "Subtitle"); assets.Add(line);
            VoiceOverDirector voice = AudioSystem.VoiceOver; voice.DuckParameter = duck; voice.DuckFadeSeconds = 1;
            voice.Play(line); yield return AudioTestUtil.WaitUntil(() => voice.DuckValue > 0.05f, "duck attack");
            AudioListener.pause = true; float before = voice.DuckValue;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(before, voice.DuckValue, 0.001f); Assert.IsTrue(voice.IsSpeaking);
        }
        [UnityTest] public IEnumerator MusicBoundaryRemainsPendingUntilResume()
        {
            MusicTrack a = MusicTrack.CreateRuntime(null, Clip(2), 120, 4); assets.Add(a);
            MusicTrack b = MusicTrack.CreateRuntime(null, Clip(2), 120, 4); assets.Add(b);
            AudioSystem.PlayMusic(a); AudioSystem.TransitionMusic(b, MusicQuantization.Immediate, 0.15f);
            double boundary = AudioSystem.Music.PendingStartDspTime;
            AudioListener.pause = true;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreSame(a, AudioSystem.Music.CurrentTrack); Assert.AreEqual(boundary, AudioSystem.Music.PendingStartDspTime);
            AudioListener.pause = false;
            yield return AudioTestUtil.WaitUntil(() => AudioSystem.Music.CurrentTrack == b && !AudioSystem.Music.IsCrossfading, "transition after resume");
        }

        [UnityTest] public IEnumerator MenuEventCanOptOutOfGlobalPause()
        {
            SoundEvent item = Event(0.2f, false); item.IgnorePause = true;
            AudioSystem.SetPaused(true); Assert.IsTrue(AudioSystem.IsPaused);
            SoundHandle handle = AudioSystem.PlayTracked(item);
            Assert.IsTrue(handle.Voice.Source.ignoreListenerPause);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "menu event during pause");
            AudioSystem.SetPaused(false);
            SoundHandle normal = AudioSystem.PlayTracked(Event(1, false));
            Assert.IsFalse(normal.Voice.Source.ignoreListenerPause);
        }
        [UnityTest] public IEnumerator NormalEventStartedDuringPauseWaitsForResume()
        {
            AudioSystem.SetPaused(true);
            SoundHandle handle = AudioSystem.PlayTracked(Event(0.2f, false));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(handle.IsPlaying);
            AudioSystem.SetPaused(false);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "paused-start event completion");
        }
    }
}
