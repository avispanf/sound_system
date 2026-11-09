using System.Collections;
using System.Linq;
using System.IO;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftCafeLifeTests
{
    private AudioMWPlayground game;
    private BadShiftCafeLife life;
    private BadShiftPoseProbe probe;
    [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);game=Object.FindFirstObjectByType<AudioMWPlayground>();life=game.Life;Assert.IsNotNull(life);probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=life.Demo.PlayerAnimator.transform;}
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var c=game.GetComponent<CharacterController>();c.enabled=false;game.transform.position=p;c.enabled=true;Physics.SyncTransforms();}
    private void Capture(string name,Vector3 focus,float size)
    {
        probe.Capture=()=>
        {
            Camera camera=game.View;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float oldSize=camera.orthographicSize;
            camera.transform.position=focus+new Vector3(3,3,-8);camera.transform.LookAt(focus);camera.orthographicSize=size;
            var target=new RenderTexture(960,640,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;Object.Destroy(image);Object.Destroy(target);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=oldSize;
        };
    }
    [UnityTest] public IEnumerator MicrowaveEPlaysAndHighlightsInsteadOfOpeningFridge()
    {
        Place(new Vector3(6,.05f,6.2f));Assert.AreEqual(BadShiftUseKind.Microwave,life.Nearest().Kind);
        var visual=life.Demo.MicrowaveVisual!=null?life.Demo.MicrowaveVisual:life.Demo.Microwave;
        Color before=visual.GetComponent<Renderer>().material.color;game.Interact();yield return new WaitForSeconds(.1f);
        Assert.Greater(visual.GetComponent<Renderer>().material.color.r,before.r);Capture("cafe-microwave",new Vector3(6,1.5f,7.8f),2.6f);Assert.Greater(life.Demo.MicrowaveResponseUntil,Time.time);Assert.IsFalse(game.FridgeDoors.Lower.IsOpen);
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>(r.EventName==life.Demo.MicrowaveBag.name||r.EventName==life.Demo.MicrowaveRandom.name)&&r.Outcome==PlaybackOutcome.Played));yield return null;
    }
    [UnityTest] public IEnumerator BathroomDoorOpensWithPlayerAgainstIt()
    {
        Place(new Vector3(-3,.05f,4.8f));Assert.AreEqual(BadShiftUseKind.Bathroom,life.Nearest().Kind);
        Vector3 start=game.transform.position;game.Interact();yield return new WaitForSeconds(1);
        Assert.IsTrue(game.Door.IsOpen);Assert.IsFalse(game.Door.IsMoving);Assert.IsFalse(game.Door.IsBlocked);
        Assert.Greater(Vector3.Distance(start,game.transform.position),.05f);Capture("cafe-bathroom",new Vector3(-3,1.5f,6.5f),3);
        Assert.IsFalse(Physics.ComputePenetration(game.Door.Leaf,game.Door.Leaf.transform.position,game.Door.Leaf.transform.rotation,game.GetComponent<CharacterController>(),game.transform.position,game.transform.rotation,out _,out _));
    }
    [UnityTest] public IEnumerator FridgeDoorsOpenAtTouchingDistance()
    {
        Bounds b=game.FridgeDoors.Lower.Leaf.bounds;Place(new Vector3(b.center.x,.05f,b.min.z-game.GetComponent<CharacterController>().radius-.02f));
        Assert.AreEqual(BadShiftUseKind.Fridge,life.Nearest().Kind);game.Interact();yield return new WaitForSeconds(1);
        Assert.IsTrue(game.FridgeDoors.Lower.IsOpen);Assert.IsFalse(game.FridgeDoors.Lower.IsMoving);
        Assert.IsTrue(game.FridgeDoors.ToggleUpperNear(game.transform.position,game.GetComponent<CharacterController>()));yield return new WaitForSeconds(1);
        Assert.IsTrue(game.FridgeDoors.Upper.IsOpen);Assert.IsFalse(game.FridgeDoors.Upper.IsMoving);Capture("cafe-fridge",new Vector3(9.4f,1.6f,7),3);yield return null;
    }
    [UnityTest] public IEnumerator BrokenCupTriggersBossAndCharacterReaction()
    {
        var cup=Object.FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None).First(p=>p.Breakable);int count=BadShiftImpactProp.BreakCount;
        cup.GetComponent<Rigidbody>().position=new Vector3(2.5f,2,-7);Physics.SyncTransforms();
        cup.GetComponent<Rigidbody>().AddForce(Vector3.down*12,ForceMode.VelocityChange);yield return new WaitForSeconds(.6f);
        Assert.Greater(BadShiftImpactProp.BreakCount,count);Assert.IsNotEmpty(life.ReactionQuote);Assert.Greater(life.BossReactionUntil,Time.time);
        Assert.IsNotNull(Object.FindFirstObjectByType<BadShiftCafeReactions>());Capture("cafe-boss",new Vector3(-8,2,6.5f),3.2f);yield return null;
    }
    [UnityTest] public IEnumerator StickDuelCanDefeatRivalAndReset()
    {
        Place(new Vector3(.25f,.05f,5.65f));Assert.AreEqual(BadShiftUseKind.Spar,life.Nearest().Kind);
        for(int i=0;i<4;i++){game.Interact();yield return new WaitForSeconds(.15f);if(i==0)Capture("cafe-duel",new Vector3(.25f,1.2f,6.5f),3);yield return new WaitForSeconds(.45f);}
        Assert.IsTrue(life.EnemyDead);Assert.IsFalse(life.PlayerDead);Assert.IsTrue(life.PlayerStick.activeSelf);
        Capture("cafe-rival-down",new Vector3(.25f,1,6.5f),3);yield return new WaitForSeconds(3.1f);Assert.AreEqual(100,life.EnemyHealth);Assert.IsFalse(life.Duel);Assert.IsFalse(life.PlayerStick.activeSelf);
    }
    [UnityTest] public IEnumerator PlayerDiesAndRespawnsWithMovementRestored()
    {
        Place(new Vector3(.25f,.05f,5.65f));game.Interact();yield return new WaitForSeconds(4.3f);
        Assert.IsTrue(life.PlayerDead);Capture("cafe-player-down",game.transform.position+Vector3.up,3);Vector3 deadPosition=game.transform.position;game.MoveCharacter(Vector3.right,true,true,.02f);Assert.Less(Vector3.Distance(deadPosition,game.transform.position),.05f);
        yield return new WaitForSeconds(3.1f);Assert.AreEqual(100,life.PlayerHealth);Assert.IsFalse(life.Duel);Assert.Less(game.transform.position.z,-6);
        Vector3 before=game.transform.position;game.MoveCharacter(Vector3.right,false,false,.1f);Assert.Greater(Vector3.Distance(before,game.transform.position),.01f);
    }
    [UnityTest] public IEnumerator SoundToggleDoesNotPauseSimulation()
    {
        float volume=AudioListener.volume,clock=Time.time;life.ToggleSound();Assert.AreEqual(0,AudioListener.volume);Assert.IsFalse(AudioListener.pause);
        yield return new WaitForSeconds(.1f);Assert.Greater(Time.time,clock);life.ToggleSound();Assert.AreEqual(volume,AudioListener.volume);
    }
}
