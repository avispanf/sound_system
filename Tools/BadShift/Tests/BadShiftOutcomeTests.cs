using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftOutcomeTests
{
    private AudioMWPlayground game;
    private BadShiftBattle battle;
    private BadShiftPoseProbe probe;
    private Transform Boss=>battle.Life.Demo.Boss;
    private Transform Arm=>Boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Broccoli pointing arm");
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();battle=game.Life.Battle;game.GetComponent<BadShiftBattleInput>().enabled=false;
        probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=battle.Life.Demo.PlayerAnimator.transform;
        Place(battle.ArenaCenter+Vector3.left*1.5f);game.Interact();yield return new WaitForSeconds(1.3f);AudioSystem.Debugger.Clear();
    }
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var body=game.GetComponent<CharacterController>();body.enabled=false;game.transform.position=p;body.enabled=true;Physics.SyncTransforms();}
    private IEnumerator Win()
    {
        for(int i=0;i<4;i++){Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();yield return new WaitForSeconds(i==3?.3f:.7f);}
        Assert.IsTrue(battle.Falling);Assert.IsFalse(battle.Celebrating(false));Assert.IsEmpty(battle.BossOutcomeQuote);
        float deadline=Time.time+2;while(!battle.Finished&&Time.time<deadline)yield return null;
        Assert.AreEqual(BadShiftBattlePhase.Victory,battle.Phase);
    }
    private void SaveTexture(RenderTexture target,string name)
    {
        var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());RenderTexture.active=old;Object.Destroy(image);
    }
    private void Capture(string name)
    {
        Camera camera=game.View;var old=camera.targetTexture;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float size=camera.orthographicSize;
        Vector3 center=(game.transform.position+battle.EnemyBody.transform.position)*.5f+Vector3.up*1.4f;
        camera.transform.position=center+new Vector3(-2,2,-7);camera.transform.LookAt(center);camera.orthographicSize=3;
        var target=new RenderTexture(1200,800,24);camera.targetTexture=target;camera.Render();SaveTexture(target,name);camera.targetTexture=old;
        camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;Object.Destroy(target);
        var portrait=game.GetComponent<BadShiftBattlePresentation>().BossPortrait as RenderTexture;Assert.IsNotNull(portrait);SaveTexture(portrait,name+"-boss");
    }
    [UnityTest] public IEnumerator WinnerCelebratesAfterFallAndKeepsResultAudioExclusive()
    {
        yield return Win();yield return new WaitForSeconds(.4f);
        Assert.IsTrue(battle.Celebrating(false));Assert.IsFalse(battle.Celebrating(true));Assert.AreEqual("Employee of the minute!",battle.BossOutcomeQuote);
        Transform winner=battle.Life.Demo.PlayerAnimator.transform.Find("Rig"),loser=battle.Life.Demo.DanceAnimator.transform.Find("Rig");
        Quaternion pose=Quaternion.identity,fallen=Quaternion.identity;
        probe.Capture=()=>{pose=winner.localRotation;fallen=loser.localRotation;Capture("outcome-victory-hop");};yield return null;
        float movement=0,fallenMovement=0,until=Time.time+1.4f;
        while(Time.time<until)
        {
            probe.Capture=()=>{movement=Mathf.Max(movement,Quaternion.Angle(pose,winner.localRotation));fallenMovement=Mathf.Max(fallenMovement,Quaternion.Angle(fallen,loser.localRotation));};yield return null;
        }
        Assert.Greater(movement,3);Assert.Less(fallenMovement,.01f);probe.Capture=()=>Capture("outcome-victory-guitar");yield return null;
        yield return new WaitForSeconds(.7f);Assert.IsTrue(battle.BossOutcomeQuote.Contains("lunch break"));Assert.AreEqual(0,AudioSystem.Music.Volume);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.VictorySound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.DefeatSound.name));
        yield return new WaitForSeconds(2.2f);Assert.IsFalse(battle.Celebrating(false));Assert.IsTrue(battle.Finished);
    }
    [UnityTest] public IEnumerator DefeatGetsRivalCelebrationFacepalmAndPauseSafePose()
    {
        Vector3 armRest=Arm.localPosition;float deadline=Time.time+14;while(!battle.Finished&&Time.time<deadline)yield return null;
        Assert.AreEqual(BadShiftBattlePhase.Defeat,battle.Phase);yield return new WaitForSeconds(.7f);
        Assert.IsTrue(battle.Celebrating(true));Assert.IsFalse(battle.Celebrating(false));Assert.IsTrue(battle.BossOutcomeQuote.Contains("nap"));
        Quaternion bossPose=Quaternion.identity;Vector3 armPose=Vector3.zero;
        probe.Capture=()=>{var eyes=Boss.GetComponentsInChildren<Transform>().Where(t=>t.name=="Sphere"&&t.GetComponent<Renderer>()!=null&&t.GetComponent<Renderer>().enabled).ToArray();Vector3 face=eyes.Aggregate(Vector3.zero,(sum,t)=>sum+t.position)/eyes.Length;Assert.Less(Vector3.Distance(face,Arm.position),.4f);Capture("outcome-defeat-facepalm");};yield return null;
        AudioSystem.SetPaused(true);Time.timeScale=0;float time=battle.OutcomeTime;
        probe.Capture=()=>{bossPose=Boss.localRotation;armPose=Arm.localPosition;};yield return null;
        yield return new WaitForSecondsRealtime(.35f);probe.Capture=()=>{Assert.AreEqual(time,battle.OutcomeTime);Assert.Less(Quaternion.Angle(bossPose,Boss.localRotation),.01f);Assert.Less(Vector3.Distance(armPose,Arm.localPosition),.001f);};yield return null;
        Time.timeScale=1;AudioSystem.SetPaused(false);yield return new WaitForSeconds(1.9f);Assert.IsTrue(battle.BossOutcomeQuote.Contains("soup"));
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.DefeatSound.name&&r.Outcome==PlaybackOutcome.Played));
    }
    [UnityTest] public IEnumerator RematchClearsOutcomeAndResetDisposesPortrait()
    {
        Vector3 armRest=Arm.localPosition;yield return Win();yield return new WaitForSeconds(.5f);
        var portrait=game.GetComponent<BadShiftBattlePresentation>().BossPortrait;Assert.IsNotNull(portrait);
        battle.Rematch();yield return null;yield return null;
        Assert.AreEqual(0,battle.OutcomeTime);Assert.IsEmpty(battle.BossOutcomeQuote);Assert.IsFalse(battle.Celebrating(false));Assert.IsFalse(battle.Celebrating(true));
        probe.Capture=()=>Assert.Less(Vector3.Distance(armRest,Arm.localPosition),.001f);yield return null;
        Assert.AreEqual(1,Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.name=="Broccoli portrait camera"));
        game.ResetScene();yield return new WaitForSeconds(.8f);Assert.IsTrue(portrait==null);
        Assert.IsFalse(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.name=="Broccoli portrait camera"));
        Assert.IsFalse(Object.FindFirstObjectByType<BadShiftBattle>().Active);
    }
}
