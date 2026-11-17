using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class ArrangementPlaybackTests
    {
        private GameObject host;
        private MusicArrangement score;
        private AudioClip source;
        private MusicArrangementPlayer player;
        [SetUp] public void SetUp()
        {
            source=AudioClip.Create("Arrangement silent fixture",48000,1,48000,false);
            score=ScriptableObject.CreateInstance<MusicArrangement>();score.Exit=2;
            for(int i=0;i<4;i++){var lane=new ArrangementLane{Name="Lane "+i};lane.Clips.Add(new ArrangementClip{Source=source,Start=0,Trim=.25,Duration=.75});score.Lanes.Add(lane);}
            host=new GameObject("Arrangement playback fixture");player=host.AddComponent<MusicArrangementPlayer>();player.Arrangement=score;
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(host);Object.DestroyImmediate(score);Object.DestroyImmediate(source);}
        [UnityTest] public IEnumerator FourLanesSeekAndStopUseOwnedSources()
        {
            Assert.IsTrue(player.Play());Assert.AreEqual(4,player.ScheduledVoiceCount);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(player.IsPlaying);Assert.That(player.Position,Is.GreaterThan(0));
            Assert.IsTrue(player.Seek(.5));Assert.AreEqual(4,player.ScheduledVoiceCount);
            Assert.IsTrue(host.GetComponentsInChildren<AudioSource>().Where(s=>s.clip!=null).All(s=>s.timeSamples>=36000));
            player.Stop();Assert.AreEqual(0,player.ScheduledVoiceCount);Assert.IsTrue(host.GetComponentsInChildren<AudioSource>().All(s=>s.clip==null));
        }
        [UnityTest] public IEnumerator SessionMuteAndSnapshotDoNotModifyAuthoredData()
        {
            Assert.IsTrue(player.Play());player.SetLaneAudition(score.Lanes[0].Id,true,false);player.Tick();
            Assert.AreEqual(1,host.GetComponentsInChildren<AudioSource>().Count(s=>s.volume==0));
            score.Lanes.Clear();yield return null;Assert.IsTrue(player.IsPlaying);Assert.AreEqual(4,player.ScheduledVoiceCount);
        }
        [UnityTest] public IEnumerator LoopContinuesAndNonLoopEnds()
        {
            score.Exit=.4;score.Loop=true;score.LoopStart=.1;score.LoopEnd=.4;
            Assert.IsTrue(player.Play());yield return new WaitForSecondsRealtime(.8f);Assert.IsTrue(player.IsPlaying);Assert.That(player.Position,Is.InRange(.1,.4));
            score.Loop=false;Assert.IsTrue(player.Play());yield return new WaitForSecondsRealtime(.65f);Assert.IsFalse(player.IsPlaying);Assert.AreEqual(.4,player.Position,1e-6);
        }
        [UnityTest] public IEnumerator PauseCancelsQueuedSourcesAndResumeUsesFrozenSnapshot()
        {
            score.Lanes[0].Clips[0].FadeIn=.1;score.Lanes[0].Clips[0].FadeOut=.1;
            Assert.IsTrue(player.Play());yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(player.Pause());double paused=player.Position;Assert.IsTrue(player.IsPaused);
            Assert.AreEqual(0,player.ScheduledVoiceCount);Assert.IsTrue(host.GetComponentsInChildren<AudioSource>().All(s=>!s.isPlaying&&s.clip==null));
            yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(paused,player.Position,1e-6);
            score.Lanes.Clear();Assert.IsTrue(player.Resume());Assert.AreEqual(4,player.ScheduledVoiceCount);
            yield return new WaitForSecondsRealtime(.2f);Assert.Greater(player.Position,paused);
            player.Stop();Assert.IsFalse(player.IsPaused);Assert.IsFalse(player.Resume());
        }
        [Test] public void InvalidPositionDoesNotScheduleAnything(){Assert.IsFalse(player.Play(double.NaN));Assert.AreEqual(0,player.ScheduledVoiceCount);}
    }
}
