using System.Collections;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftAudioLabelTests
{
    private AudioMWPlayground game;
    private BadShiftDemo demo;
    private BadShiftAudioGuide guide;
    private SoundEvent sound;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField"); yield return new WaitForSeconds(1);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();game.enabled=false;demo=game.GetComponent<BadShiftDemo>();guide=game.GetComponent<BadShiftAudioGuide>();
        sound=SoundEvent.CreateRuntime(game.Impact.Clips);sound.name="Step_Test";sound.SpatialBlend=1;sound.Volume=.1f;sound.MinDistance=1;sound.MaxDistance=20;
        AudioSystem.Debugger.Clear();yield return new WaitForSeconds(BadShiftAudioGuide.LabelLifetime+.1f);
    }
    [UnityTearDown] public IEnumerator Cleanup(){AudioSystem.StopAll();Object.Destroy(sound);yield return PlaygroundTestCleanup.Clear();}
    [UnityTest] public IEnumerator SuccessfulNearbyEventAppearsOnceThenExpires()
    {
        Assert.IsTrue(demo.AudioLabelsVisible);Assert.IsFalse(demo.AudioMapVisible);
        AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);yield return null;Assert.AreEqual("Step_Test",guide.LatestLabel);int count=guide.LabelCount;
        yield return new WaitForSeconds(.2f);Assert.AreEqual(count,guide.LabelCount);
        yield return new WaitForSeconds(BadShiftAudioGuide.LabelLifetime);Assert.AreEqual(0,guide.LabelCount);
    }
    [UnityTest] public IEnumerator DistantLoopingAndRejectedRequestsDoNotCreateLabels()
    {
        AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position+Vector3.right*12);
        AudioSystem.Play(null);sound.Loop=true;AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);yield return null;
        Assert.AreEqual(0,guide.LabelCount);
    }
    [UnityTest] public IEnumerator OffDiscardsHistoryAndOneButtonCyclesLabelsDetailsOff()
    {
        demo.CycleAudioVisuals();Assert.IsTrue(demo.AudioMapVisible);demo.CycleAudioVisuals();Assert.IsFalse(demo.AudioLabelsVisible);
        AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);yield return null;Assert.AreEqual(0,guide.LabelCount);
        demo.CycleAudioVisuals();Assert.IsTrue(demo.AudioLabelsVisible);Assert.IsFalse(demo.AudioMapVisible);yield return null;Assert.AreEqual(0,guide.LabelCount);
        AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);yield return null;Assert.AreEqual("Step_Test",guide.LatestLabel);
    }
    [UnityTest] public IEnumerator FullDebuggerBufferAndClearDoNotReplayOldLabels()
    {
        var debugger=AudioSystem.Debugger;int capacity=debugger.Capacity;debugger.Capacity=2;
        for(int i=0;i<5;i++)AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);
        yield return null;Assert.AreEqual(1,guide.LabelCount);
        yield return new WaitForSeconds(BadShiftAudioGuide.LabelLifetime+.1f);Assert.AreEqual(0,guide.LabelCount);
        debugger.Clear();AudioSystem.PlayTrackedAtPosition(sound,game.Listener.position);yield return null;Assert.AreEqual(1,guide.LabelCount);debugger.Capacity=capacity;
    }
    [Test] public void ConnectionsFadeByWorldDistanceAndScreenLength()
    {
        Assert.AreEqual(1,BadShiftAudioGuide.ConnectionStrength(2,100));Assert.AreEqual(0,BadShiftAudioGuide.ConnectionStrength(6,100));Assert.AreEqual(0,BadShiftAudioGuide.ConnectionStrength(2,300));
        Assert.That(BadShiftAudioGuide.ConnectionStrength(4,200),Is.InRange(.01f,.99f));Assert.AreEqual(0,BadShiftAudioGuide.LabelDistanceAlpha(8));
    }
}
