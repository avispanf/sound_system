using System.Collections;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class BadShiftAcousticsTests
{
    private BadShiftDemo demo;
    private BadShiftSoundscape sc;

    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");
        yield return new WaitForSeconds(.45f);
        demo = Object.FindFirstObjectByType<BadShiftDemo>();
        sc = demo.Game.Soundscape;
    }

    [UnityTearDown] public IEnumerator Clean() { yield return PlaygroundTestCleanup.Clear(); }

    private void Move(Vector3 position)
    {
        demo.Game.RespawnAt(new Vector3(position.x,.08f,position.z),Quaternion.identity);
        Physics.SyncTransforms();
    }

    [UnityTest] public IEnumerator CompressorRunsOnSfxWithEnergyAndRecoversAStolenVoice()
    {
        Move(demo.Fridge.position + Vector3.back * 2);
        yield return new WaitForSeconds(.3f);
        var voice = demo.FridgeHandle.Voice;
        Assert.IsNotNull(voice);
        var source = voice.Source;
        Assert.AreEqual("FridgeCompressor",source.clip.name);
        Assert.AreSame(demo.Fridge,voice.AttachTarget);
        Assert.AreEqual("SFX",source.outputAudioMixerGroup.name);
        Assert.IsTrue(source.isPlaying && source.loop);
        Assert.Greater(source.volume,.5f);
        Assert.AreEqual(AudioRolloffMode.Linear,source.rolloffMode);
        var clip = new float[8192];
        Assert.IsTrue(source.clip.GetData(clip,0));
        float squares=0;
        foreach(float sample in clip) squares+=sample*sample;
        Assert.Greater(Mathf.Sqrt(squares/clip.Length)*source.volume,.12f);
        int sampleTime=source.timeSamples;
        yield return new WaitForSeconds(.12f);
        Assert.AreNotEqual(sampleTime,source.timeSamples);
        var guide=demo.GetComponent<BadShiftAudioGuide>();
        float near=guide.DistanceGain;
        Assert.Greater(near,.85f);
        Move(demo.Fridge.position + Vector3.back*10);
        yield return new WaitForSeconds(.25f);
        Assert.Less(guide.DistanceGain,.06f);
        demo.FridgeHandle.Stop();
        yield return new WaitForSeconds(1);
        Assert.IsTrue(demo.FridgeHandle.IsPlaying);
    }

    [UnityTest] public IEnumerator DoorFiltersTheSameRunningTapAtOneListenerPosition()
    {
        var door = demo.Game.Door;
        door.SetOpen(true);
        Move(sc.Room.TransformPoint(new Vector3(-1,0,.54f)));
        yield return new WaitForSeconds(.6f);
        demo.Game.Interact();
        var water=sc.Water;
        Assert.IsTrue(water.IsPlaying);
        Move(sc.Room.TransformPoint(new Vector3(1.22f,0,-3.48f)));
        yield return new WaitForSeconds(1.4f);
        float openGain=water.Voice.OcclusionVolume;
        Assert.Greater(openGain,.87f);
        var spot=demo.Game.Listener.position;
        Assert.AreEqual(BadShiftUseKind.Bathroom,demo.Game.Life.Nearest().Kind);
        demo.Game.Interact();
        yield return new WaitForSeconds(1.8f);
        Assert.IsFalse(door.IsOpen || door.IsMoving);
        Assert.AreEqual(water.Id,sc.Water.Id);
        Assert.Less(Vector3.Distance(spot,demo.Game.Listener.position),.06f);
        Assert.Less(water.Voice.OcclusionVolume,.33f);
        var filter=water.Voice.Source.GetComponent<AudioLowPassFilter>();
        Assert.IsTrue(filter.enabled);
        Assert.Less(filter.cutoffFrequency,1900);
        demo.Game.Interact();
        yield return new WaitForSeconds(1.8f);
        Assert.Greater(water.Voice.OcclusionVolume,.87f);
        Assert.IsTrue(!filter.enabled || filter.cutoffFrequency>17500);
    }

    [UnityTest] public IEnumerator DancerDozesWakesNearbyAndSleepsAgainAtNight()
    {
        sc.Sky.SetMode(BadShiftSkyMode.Night);
        Move(new Vector3(-9,0,-8));
        yield return new WaitForSeconds(2);
        Assert.IsTrue(demo.DancerSleeping && demo.BossSleeping);
        Assert.IsFalse(demo.DancerMusicHandle.IsPlaying);
        Move(demo.Boss.position+Vector3.back*1.5f);
        yield return new WaitForSeconds(.4f);
        Assert.IsFalse(AudioSystem.VoiceOver.IsSpeaking);
        Move(demo.Dancer.position+Vector3.left*2);
        yield return new WaitForSeconds(.4f);
        Assert.IsTrue(demo.EmployeeDancing && !demo.DancerSleeping && demo.BossSleeping);
        Assert.Less(demo.DanceAnimator.speed,.6f);
        Assert.IsTrue(demo.DancerMusicHandle.IsPlaying);
        Move(new Vector3(-9,0,-8));
        yield return new WaitForSeconds(.45f);
        Assert.IsTrue(demo.DancerSleeping);
        Assert.IsFalse(demo.DancerMusicHandle.IsPlaying);
        sc.Sky.SetMode(BadShiftSkyMode.Day);
        yield return new WaitForSeconds(1.6f);
        Assert.IsFalse(demo.BossSleeping || demo.DancerSleeping);
        Assert.AreEqual(1,demo.DanceAnimator.speed);
    }
}
