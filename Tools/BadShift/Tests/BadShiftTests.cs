using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftTests
{
    private BadShiftDemo demo;
    [UnitySetUp] public IEnumerator Load() { yield return SceneManager.LoadSceneAsync("RelayField"); yield return null;demo=Object.FindFirstObjectByType<BadShiftDemo>();Assert.IsNotNull(demo); }
    [UnityTearDown] public IEnumerator Stop(){AudioSystem.Shutdown();yield return null;}
    [UnityTest] public IEnumerator ShuffleBagShowsActualDistinctSelections()
    {
        var clips=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<demo.MicrowaveBag.Clips.Length;i++){demo.PlayMicrowave();clips.Add(demo.LastAnswer);yield return null;}
        Assert.AreEqual(demo.MicrowaveBag.Clips.Length,clips.Count);
        demo.ToggleSelection();demo.PlayMicrowave();Assert.IsTrue(demo.MicrowaveRandom.Clips.Any(c=>c.name==demo.LastAnswer));
    }
    [UnityTest] public IEnumerator ProcessingUsesSameClipAndRealFilters()
    {
        AudioClip clip=demo.RoomSource.clip;
        for(int i=0;i<3;i++){demo.SetEffect(i);Assert.AreEqual(i==1,demo.Echo.enabled);Assert.AreEqual(i==2,demo.Reverb.enabled);demo.PlayRoom();yield return null;Assert.AreSame(clip,demo.RoomSource.clip);Assert.IsTrue(demo.RoomSource.isPlaying);}
    }
    [UnityTest] public IEnumerator StaffReactionWaitsForMusicAndFreezesDuringVoice()
    {
        Assert.IsFalse(demo.Game.transform.Find("Player body").GetComponent<Renderer>().enabled);
        Assert.IsNotNull(demo.PlayerAnimator.runtimeAnimatorController);
        demo.Game.RequestMusic(true);yield return null;
        if(AudioSystem.Music.IsTransitionPending)Assert.AreEqual(0,demo.DanceAnimator.speed);
        float until=Time.realtimeSinceStartup+6;
        while(AudioSystem.Music.IsTransitionPending && Time.realtimeSinceStartup<until)yield return null;
        yield return null;Assert.AreEqual("Combat",AudioSystem.Music.CurrentTrack.name);Assert.AreEqual(1,demo.DanceAnimator.speed);
        demo.Game.RequestMusic(false);yield return null;
        if(AudioSystem.Music.IsTransitionPending)Assert.AreEqual(1,demo.DanceAnimator.speed);
        demo.Game.Speak();yield return null;Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);Assert.AreEqual(0,demo.DanceAnimator.speed);
    }
    [UnityTest] public IEnumerator FridgeStaysFixedAndMeasuresPlayerListener()
    {
        Vector3 fixedPosition=demo.Fridge.position;float before=demo.ListenerDistance;
        CharacterController c=demo.Game.GetComponent<CharacterController>();c.enabled=false;demo.Game.transform.position=demo.Fridge.position+Vector3.back*2;c.enabled=true;yield return null;
        Assert.AreEqual(fixedPosition,demo.Fridge.position);Assert.Less(demo.ListenerDistance,before);
        Assert.AreEqual(Vector3.Distance(demo.Game.Listener.position,fixedPosition),demo.ListenerDistance,.001f);
    }
}
