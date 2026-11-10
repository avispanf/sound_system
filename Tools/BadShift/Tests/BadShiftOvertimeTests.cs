using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftOvertimeTests
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
    private IEnumerator Hit(float wait)
    {
        Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();yield return new WaitForSeconds(wait);
    }
    private IEnumerator Enter()
    {
        yield return Hit(.7f);Assert.AreEqual(75,battle.EnemyHealth);Assert.IsFalse(battle.SecondPhase);
        yield return Hit(.25f);Assert.AreEqual(50,battle.EnemyHealth);Assert.IsTrue(battle.SecondPhase);Assert.IsTrue(battle.PhaseTwoIntro);
    }
    private IEnumerator Windup(BadShiftEnemyAttack kind)
    {
        float until=Time.time+5;while(!battle.EnemyWindingUp&&Time.time<until)yield return null;
        Assert.IsTrue(battle.EnemyWindingUp,"No overtime windup for "+kind);Assert.AreEqual(kind,battle.EnemyAttack);
    }
    private IEnumerator FinishSwing()
    {
        float until=Time.time+2;while((battle.EnemyWindingUp||Time.time<battle.EnemySwingUntil)&&Time.time<until)yield return null;
        Assert.IsFalse(battle.EnemyWindingUp);Assert.LessOrEqual(battle.EnemySwingUntil,Time.time);
    }
    private static readonly BadShiftEnemyAttack[][] Menu={new[]{BadShiftEnemyAttack.Sweep,BadShiftEnemyAttack.Backhand,BadShiftEnemyAttack.Overhead},new[]{BadShiftEnemyAttack.Thrust,BadShiftEnemyAttack.Overhead}};
    [UnityTest] public IEnumerator ThresholdRunsOnceAndTantrumPausesWithoutDamagingPlayer()
    {
        yield return Enter();Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.ChallengeSound.name));
        Assert.IsFalse(battle.EnemyWindingUp);Assert.AreEqual(100,battle.PlayerHealth);Assert.IsFalse(battle.LocksMovement);
        AudioSystem.SetPaused(true);Time.timeScale=0;float t=battle.PhaseTwoIntroProgress;
        yield return new WaitForSecondsRealtime(.25f);Assert.AreEqual(t,battle.PhaseTwoIntroProgress);
        Time.timeScale=1;AudioSystem.SetPaused(false);yield return new WaitForSeconds(.4f);
        probe.Capture=()=>Capture("overtime-tantrum");yield return null;
        Assert.Greater(game.GetComponent<BadShiftBattlePresentation>().OvertimeWeight,.9f);
        yield return Hit(.25f);Assert.AreEqual(25,battle.EnemyHealth);Assert.Greater(battle.PhaseTwoIntroProgress,t);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.ChallengeSound.name));
        battle.Rematch();yield return new WaitForSeconds(.6f);Assert.IsFalse(battle.SecondPhase);Assert.IsFalse(battle.PhaseTwoIntro);Assert.AreEqual(0,battle.EnemyComboLength);
        Assert.AreEqual(0,game.GetComponent<BadShiftBattlePresentation>().OvertimeWeight,.001f);
    }
    [UnityTest] public IEnumerator BothNewCombosLandFiveNaturalContactsWithSeparateWindups()
    {
        yield return Enter();AudioSystem.Debugger.Clear();int hits=0;
        for(int pattern=0;pattern<Menu.Length;pattern++)
        {
            Vector3 aim=Vector3.zero;
            for(int step=0;step<Menu[pattern].Length;step++)
            {
                yield return Windup(Menu[pattern][step]);Assert.AreEqual(step+1,battle.EnemyComboStep);Assert.AreEqual(Menu[pattern].Length,battle.EnemyComboLength);
                if(step==0)aim=battle.EnemyAim;else Assert.Less(Vector3.Distance(aim,battle.EnemyAim),.001f);
                int hp=battle.PlayerHealth;yield return new WaitForSeconds(.22f);Assert.AreEqual(hp,battle.PlayerHealth);
                string name="overtime-"+(pattern==0?"triple-":"rush-")+(step+1);probe.Capture=()=>Capture(name);yield return null;
                yield return FinishSwing();hits++;Assert.AreEqual(100-hits*20,battle.PlayerHealth,"Overtime contact "+hits);
            }
        }
        Assert.AreEqual(5,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.HitSound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.IsTrue(battle.Falling);Assert.IsFalse(battle.PhaseTwoIntro);Assert.AreEqual(0,battle.EnemyComboLength);
    }
    [UnityTest] public IEnumerator BlockingAnyStepCancelsTheRemainingOvertimeCombo()
    {
        yield return Enter();AudioSystem.Debugger.Clear();int hits=0,blocks=0;int[] stopAt={1,1,2,2,3};
        for(int round=0;round<stopAt.Length;round++)
        {
            battle.SetBlocking(false);var moves=Menu[round%2];
            for(int step=1;step<=stopAt[round];step++)
            {
                yield return Windup(moves[step-1]);
                if(step<stopAt[round]){yield return FinishSwing();hits++;Assert.AreEqual(100-hits*20,battle.PlayerHealth);continue;}
                battle.SetBlocking(true);float until=Time.time+2;while(battle.BlockFeedback==0&&Time.time<until)yield return null;
                Assert.Greater(battle.BlockFeedback,0,"No block in round "+round+", step "+step+", distance "+Vector3.Distance(game.transform.position,battle.EnemyBody.transform.position));Assert.AreEqual(100-hits*20,battle.PlayerHealth);
                Assert.AreEqual(0,battle.EnemyComboLength);Assert.AreEqual(0,battle.EnemyComboStep);Assert.IsFalse(battle.EnemyWindingUp);blocks++;
            }
        }
        Assert.AreEqual(5,blocks);Assert.AreEqual(5,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.BlockSound.name&&r.Outcome==PlaybackOutcome.Played));
    }
    [UnityTest] public IEnumerator WalkingOutOfEachWindupAvoidsBothOvertimeCombos()
    {
        yield return Enter();AudioSystem.Debugger.Clear();
        foreach(var moves in Menu)
        {
            Vector3 aim=Vector3.zero;
            for(int step=0;step<moves.Length;step++)
            {
                yield return Windup(moves[step]);if(step==0)aim=battle.EnemyAim;else Assert.Less(Vector3.Distance(aim,battle.EnemyAim),.001f);
                game.enabled=false;float until=Time.time+.6f;Vector3 side=Vector3.Cross(Vector3.up,aim);
                while(Time.time<until){game.MoveCharacter(side,false,false,Time.deltaTime);yield return null;}game.enabled=true;
                yield return FinishSwing();Assert.AreEqual(100,battle.PlayerHealth,"Dodge failed for "+battle.EnemyAttackName);
            }
            Place(battle.EnemyBody.transform.position+battle.EnemyAim*1.4f);
        }
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name));
    }
    [UnityTest] public IEnumerator TantrumIsVulnerableAndDeathThenResetClearThePhase()
    {
        yield return Enter();yield return new WaitForSeconds(.4f);Assert.IsTrue(battle.PhaseTwoIntro);
        yield return Hit(.25f);Assert.AreEqual(25,battle.EnemyHealth);
        yield return new WaitForSeconds(.45f);yield return Hit(.25f);Assert.AreEqual(0,battle.EnemyHealth);Assert.IsTrue(battle.Falling);Assert.IsFalse(battle.PhaseTwoIntro);
        float until=Time.time+2;while(!battle.Finished&&Time.time<until)yield return null;Assert.AreEqual(BadShiftBattlePhase.Victory,battle.Phase);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.VictorySound.name));
        game.ResetScene();yield return new WaitForSeconds(.8f);battle=Object.FindFirstObjectByType<BadShiftBattle>();
        Assert.IsFalse(battle.SecondPhase);Assert.IsFalse(battle.Active);Assert.AreEqual(0,battle.EnemyComboLength);Assert.AreEqual(100,battle.EnemyHealth);
    }
}
