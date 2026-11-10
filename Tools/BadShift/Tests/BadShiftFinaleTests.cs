using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftFinaleTests
{
    private AudioMWPlayground game;
    private BadShiftDemo demo;
    private BadShiftPoseProbe probe;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();demo=game.GetComponent<BadShiftDemo>();
        probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=demo.PlayerAnimator.transform;
    }
    [UnityTearDown] public IEnumerator Cleanup(){yield return PlaygroundTestCleanup.Clear();}
    private void Capture(string name)
    {
        var camera=game.View;var previous=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(1600,900,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());
        camera.targetTexture=previous;RenderTexture.active=active;Object.Destroy(image);Object.Destroy(target);
    }
    private void CheckActor()
    {
        foreach(var r in demo.PlayerAnimator.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy))
        {
            Assert.IsFalse(float.IsNaN(r.bounds.size.x),r.name);Assert.Less(r.bounds.size.magnitude,4,r.name+" stretched");
            Assert.Less(Vector3.Distance(r.bounds.center,game.transform.position),4,r.name+" detached");
        }
        Assert.That(demo.PlayerAnimator.transform.localScale.y,Is.GreaterThan(.1f));
    }
    [UnityTest] public IEnumerator RepeatedFurnitureImpactsAndLandingChatterKeepClayBounded()
    {
        game.enabled=false;
        foreach(float z in new[]{-4.8f,-1.6f,1.6f,5.7f})
        {
            game.RespawnAt(new Vector3(-9.9f,.05f,z),Quaternion.identity);yield return null;
            float until=Time.time+1.2f;
            while(Time.time<until){game.MoveCharacter(Vector3.right,game.IsGrounded,true,Time.deltaTime);probe.Capture=CheckActor;yield return null;}
        }
        game.RespawnAt(new Vector3(-6,.05f,-7),Quaternion.identity);
        for(int i=0;i<160;i++)
        {
            if(i%4==0)game.RespawnAt(new Vector3(-6,.08f,-7),Quaternion.identity);
            game.MoveCharacter(Vector3.zero,false,false,Time.deltaTime);probe.Capture=CheckActor;yield return null;
        }
        probe.Capture=()=>Capture("finale-furniture");yield return null;
    }
    [UnityTest] public IEnumerator InvisibleBoundariesContainSprintJumpAndLargeKnockback()
    {
        game.enabled=false;
        Vector3[] start={new Vector3(-10,2,-7),new Vector3(10,2,-7),new Vector3(3,2,-8),new Vector3(3,5,8)};
        Vector3[] direction={Vector3.left,Vector3.right,Vector3.back,Vector3.forward};
        for(int edge=0;edge<4;edge++)
        {
            game.RespawnAt(start[edge],Quaternion.identity);game.AddImpulse(direction[edge]*35);
            float until=Time.time+.8f;
            while(Time.time<until){game.MoveCharacter(direction[edge],game.IsGrounded,true,Time.deltaTime);yield return null;Assert.Less(Mathf.Abs(game.transform.position.x),11.9f);Assert.Less(Mathf.Abs(game.transform.position.z),9.9f);Assert.Greater(game.transform.position.y,-.2f);}
        }
        var edges=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name=="Invisible level boundary").ToArray();Assert.AreEqual(4,edges.Length);Assert.IsTrue(edges.All(c=>c.GetComponent<Renderer>()==null&&!c.isTrigger));
    }
    [UnityTest] public IEnumerator LayoutHasCenteredBathroomInsetLetteringAndNoExitMarker()
    {
        Assert.AreEqual(new Vector3(0,0,2),demo.Room.position);Assert.IsNull(GameObject.Find("Pendant cable"));Assert.IsNull(GameObject.Find("Coral pendant shade"));Assert.IsNull(GameObject.Find("Exit floor label"));
        Assert.IsTrue(game.Exit.GetComponentsInChildren<Renderer>().All(r=>!r.enabled));
        var band=GameObject.Find("Cafe sign cream band").GetComponent<Renderer>().bounds;var label=GameObject.Find("Cafe lettering").GetComponent<Renderer>().bounds;
        Assert.AreEqual(band.center.x,label.center.x,.015f);Assert.AreEqual(band.center.y,label.center.y,.015f);Assert.Greater(label.min.x-band.min.x,.18f);Assert.Greater(band.max.x-label.max.x,.18f);
        probe.Capture=()=>Capture("finale-layout");yield return null;
    }
    [UnityTest] public IEnumerator ResetRestoresNormalSoundAndTelemetryUsesRuntimeValues()
    {
        Assert.IsFalse(BadShiftSilentLaunch.Requested);Assert.IsFalse(game.Life.Muted);Assert.AreEqual(1,AudioListener.volume);
        game.Life.ToggleSound();Assert.AreEqual(0,AudioListener.volume);game.ResetScene();yield return new WaitForSeconds(.8f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();demo=game.GetComponent<BadShiftDemo>();Assert.IsFalse(game.Life.Muted);Assert.AreEqual(1,AudioListener.volume);
        var guide=game.GetComponent<BadShiftAudioGuide>();Assert.IsNotNull(guide);Assert.IsTrue(demo.AudioLabelsVisible);Assert.IsFalse(demo.AudioMapVisible);
        game.RespawnAt(new Vector3(8,.05f,5),Quaternion.identity);yield return new WaitForSeconds(.1f);float near=guide.DistanceGain;
        game.RespawnAt(new Vector3(8,.05f,-7),Quaternion.identity);yield return new WaitForSeconds(.1f);Assert.Less(guide.DistanceGain,near);
        game.Speak();yield return new WaitForSeconds(.4f);Assert.IsTrue(guide.HasMixerDuck);Assert.Less(guide.MixerDuckDb,-1);
        demo.PlayMicrowave();Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==demo.MicrowaveBag.name&&r.ClipName==demo.LastAnswer));
    }
    [UnityTest] public IEnumerator OvertimeFinisherFallsBeforeGoldVictoryAndCleansOnRematch()
    {
        var battle=game.Life.Battle;game.GetComponent<BadShiftBattleInput>().enabled=false;
        game.RespawnAt(battle.ArenaCenter+Vector3.left*1.5f,Quaternion.identity);game.Interact();yield return new WaitForSeconds(1.3f);
        for(int i=0;i<4;i++){game.RespawnAt(battle.EnemyBody.transform.position+Vector3.left*1.5f,Quaternion.identity);battle.Attack();yield return new WaitForSeconds(i<3?.7f:.25f);}
        Assert.IsTrue(battle.OvertimeFinish);Assert.IsTrue(battle.Falling);Assert.IsFalse(battle.Finished);float hp=battle.PlayerHealth;
        yield return new WaitForSeconds(.3f);probe.Capture=()=>Capture("finale-finisher");yield return null;
        Assert.IsTrue(game.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Overtime confetti")&&l.enabled));
        AudioSystem.SetPaused(true);Time.timeScale=0;float progress=battle.FallProgress;yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual(progress,battle.FallProgress);Time.timeScale=1;AudioSystem.SetPaused(false);
        yield return new WaitForSeconds(1.5f);Assert.AreEqual(BadShiftBattlePhase.Victory,battle.Phase);Assert.AreEqual(hp,battle.PlayerHealth);Assert.AreEqual(0,AudioSystem.Music.Volume);
        probe.Capture=()=>Capture("finale-victory");yield return null;
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.VictorySound.name));
        battle.Rematch();yield return null;Assert.IsFalse(battle.OvertimeFinish);Assert.IsFalse(game.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Overtime confetti")&&l.enabled));
    }
}
