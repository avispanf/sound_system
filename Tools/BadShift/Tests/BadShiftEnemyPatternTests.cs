using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftEnemyPatternTests
{
    private AudioMWPlayground game;
    private BadShiftBattle battle;
    private BadShiftPoseProbe probe;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();battle=game.Life.Battle;game.GetComponent<BadShiftBattleInput>().enabled=false;
        probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=battle.Life.Demo.PlayerAnimator.transform;
        Place(battle.ArenaCenter+Vector3.left*1.5f);game.Interact();yield return new WaitForSeconds(1.3f);AudioSystem.Debugger.Clear();
    }
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var body=game.GetComponent<CharacterController>();body.enabled=false;game.transform.position=p;body.enabled=true;Physics.SyncTransforms();}
    private void Capture(string name)
    {
        var camera=game.View;var old=camera.targetTexture;var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;
        Vector3 center=(game.transform.position+battle.EnemyBody.transform.position)*.5f+Vector3.up*1.3f;
        camera.transform.position=center+new Vector3(-2,2,-7);camera.transform.LookAt(center);camera.orthographicSize=2.8f;
        var target=new RenderTexture(1200,800,24);camera.targetTexture=target;camera.Render();var active=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;Object.Destroy(target);Object.Destroy(image);
    }
    private IEnumerator Windup(BadShiftEnemyAttack expected)
    {
        float deadline=Time.time+4;
        while(!battle.EnemyWindingUp&&Time.time<deadline)yield return null;
        Assert.IsTrue(battle.EnemyWindingUp,"Enemy did not prepare "+expected);Assert.AreEqual(expected,battle.EnemyAttack);
    }
    private IEnumerator FinishSwing()
    {
        float deadline=Time.time+2;
        while((battle.EnemyWindingUp||Time.time<battle.EnemySwingUntil)&&Time.time<deadline)yield return null;
        Assert.IsFalse(battle.EnemyWindingUp);Assert.LessOrEqual(battle.EnemySwingUntil,Time.time);
    }
    private static readonly BadShiftEnemyAttack[] Patterns={BadShiftEnemyAttack.Sweep,BadShiftEnemyAttack.Backhand,BadShiftEnemyAttack.Overhead,BadShiftEnemyAttack.Thrust};
    [UnityTest] public IEnumerator FourDistinctWindupsConnectOnceThroughTheVisibleWeapon()
    {
        Quaternion last=Quaternion.identity;int hits=0;
        foreach(var kind in Patterns)
        {
            yield return Windup(kind);Assert.AreEqual(hits<2?hits+1:0,battle.EnemyComboStep);
            if(hits==0)Place(battle.EnemyBody.transform.position+battle.EnemyAim*1.5f);
            int health=battle.PlayerHealth;yield return new WaitForSeconds(.22f);Assert.AreEqual(health,battle.PlayerHealth);
            string name="enemy-windup-"+kind.ToString().ToLowerInvariant();
            probe.Capture=()=>{var rotation=battle.Life.EnemyStick.transform.rotation;if(hits>0)Assert.Greater(Quaternion.Angle(last,rotation),15);last=rotation;Capture(name);};yield return null;
            yield return FinishSwing();hits++;Assert.AreEqual(100-hits*20,battle.PlayerHealth,"No physical contact for "+kind);
        }
        Assert.AreEqual(4,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.HitSound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.AreEqual(4,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.AttackSound.name&&r.Outcome==PlaybackOutcome.Played));Assert.IsTrue(game.IsGrounded);
    }
    [UnityTest] public IEnumerator GuardBlocksEveryPatternAndCancelsSecondHelping()
    {
        battle.SetBlocking(true);int blocks=0;
        foreach(var kind in new[]{BadShiftEnemyAttack.Sweep,BadShiftEnemyAttack.Overhead,BadShiftEnemyAttack.Thrust})
        {
            yield return Windup(kind);Place(battle.EnemyBody.transform.position+battle.EnemyAim*1.4f);game.transform.rotation=Quaternion.LookRotation(-battle.EnemyAim);
            float deadline=Time.time+2;while(battle.BlockFeedback==0&&Time.time<deadline)yield return null;
            Assert.Greater(battle.BlockFeedback,0,"No blocked contact for "+kind);blocks++;
            Assert.AreEqual(100,battle.PlayerHealth);Assert.AreEqual(100,battle.EnemyHealth);Assert.AreEqual(0,battle.EnemyComboStep);Assert.AreEqual(BadShiftEnemyAttack.None,battle.EnemyAttack);
            Assert.AreEqual(blocks,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.BlockSound.name&&r.Outcome==PlaybackOutcome.Played));
            yield return new WaitForSeconds(.65f);
        }
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name));
    }
    [UnityTest] public IEnumerator SidestepsAvoidTheCommittedDoubleAndBothHeavyAttacks()
    {
        Vector3 firstAim=Vector3.zero;
        foreach(var kind in Patterns)
        {
            yield return Windup(kind);Vector3 aim=battle.EnemyAim;Quaternion facing=battle.EnemyBody.transform.rotation;
            if(kind==BadShiftEnemyAttack.Sweep)firstAim=aim;
            if(kind==BadShiftEnemyAttack.Backhand)Assert.Less(Vector3.Distance(firstAim,aim),.001f);
            if(kind!=BadShiftEnemyAttack.Backhand)Place(battle.EnemyBody.transform.position+aim*1.4f);
            game.enabled=false;
            Vector3 side=Vector3.Cross(Vector3.up,aim);float until=Time.time+.6f;
            while(Time.time<until){game.MoveCharacter(side,false,false,Time.deltaTime);yield return null;}
            game.enabled=true;
            Assert.Less(Quaternion.Angle(facing,battle.EnemyBody.transform.rotation),.01f);Assert.Less(Vector3.Distance(aim,battle.EnemyAim),.001f);
            yield return FinishSwing();Assert.AreEqual(100,battle.PlayerHealth,"Dodge failed for "+kind);
            if(kind!=BadShiftEnemyAttack.Sweep)Place(battle.EnemyBody.transform.position+battle.EnemyAim*1.4f);
        }
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name));
    }
    [UnityTest] public IEnumerator WindupPausesCanBeInterruptedAndRematchClearsTells()
    {
        yield return Windup(BadShiftEnemyAttack.Sweep);yield return new WaitForSeconds(.15f);
        var tell=game.GetComponentsInChildren<LineRenderer>().First(l=>l.name=="Enemy attack direction");Assert.IsTrue(tell.enabled);
        AudioSystem.SetPaused(true);Time.timeScale=0;float progress=battle.EnemyWindupProgress;Vector3 tip=tell.GetPosition(8);
        yield return new WaitForSecondsRealtime(.25f);Assert.AreEqual(progress,battle.EnemyWindupProgress);Assert.AreEqual(tip,tell.GetPosition(8));
        Time.timeScale=1;AudioSystem.SetPaused(false);battle.Attack();yield return new WaitForSeconds(.25f);
        Assert.AreEqual(75,battle.EnemyHealth);Assert.IsFalse(battle.EnemyWindingUp);Assert.AreEqual(0,battle.EnemyComboStep);Assert.IsFalse(tell.enabled);
        battle.Rematch();yield return null;Assert.AreEqual(BadShiftEnemyAttack.None,battle.EnemyAttack);Assert.AreEqual(0,battle.EnemyWindupProgress);Assert.IsFalse(tell.enabled);
        yield return new WaitForSeconds(1.3f);yield return Windup(BadShiftEnemyAttack.Sweep);
        battle.ReturnToCafe();yield return null;Assert.IsFalse(tell.enabled);Assert.AreEqual(BadShiftEnemyAttack.None,battle.EnemyAttack);
    }
    [UnityTest] public IEnumerator WallsPreventDamageForEveryEnemyPattern()
    {
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(game.transform.position+battle.EnemyBody.transform.position)*.5f+Vector3.up;
        wall.transform.localScale=new Vector3(.15f,2,3);Physics.SyncTransforms();
        foreach(var kind in Patterns){yield return Windup(kind);yield return FinishSwing();Assert.AreEqual(100,battle.PlayerHealth,"Hit through wall: "+kind);}
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name));Object.Destroy(wall);
    }
}
