using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftMeleeTests
{
    private AudioMWPlayground game;
    private BadShiftBattle battle;
    private Vector2 Center=>new Vector2(Screen.width*.5f,Screen.height*.5f);
    [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);game=Object.FindFirstObjectByType<AudioMWPlayground>();battle=game.Life.Battle;game.GetComponent<BadShiftBattleInput>().enabled=false;AudioSystem.Debugger.Clear();}
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var c=game.GetComponent<CharacterController>();c.enabled=false;game.transform.position=p;c.enabled=true;Physics.SyncTransforms();}
    private IEnumerator Begin(){Place(battle.ArenaCenter+Vector3.left*1.5f);game.Interact();Assert.IsNotEmpty(battle.EnemyTaunt);yield return new WaitForSeconds(1.3f);}
    [UnityTest] public IEnumerator MouseAttacksBlockAndHudClicksRemainDistinct()
    {
        yield return Begin();game.Interact();Assert.AreEqual(0,battle.PlayerSwingUntil);
        battle.ReadPointer(true,false,new Vector2(10,Screen.height-10));Assert.AreEqual(0,battle.PlayerSwingUntil);
        battle.ReadPointer(true,false,Center);Assert.Greater(battle.PlayerSwingUntil,Time.time);
        battle.ReadPointer(false,true,Center);Assert.IsFalse(battle.IsBlocking);yield return new WaitForSeconds(.4f);Assert.AreEqual(75,battle.EnemyHealth);
        battle.Rematch();yield return new WaitForSeconds(1.3f);
        float until=Time.time+2;while(Time.time<until){battle.ReadPointer(false,true,Center);Assert.IsTrue(battle.IsBlocking);yield return null;}
        Assert.AreEqual(100,battle.PlayerHealth);Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.BlockSound.name&&r.Outcome==PlaybackOutcome.Played));
        battle.ReadPointer(false,false,Center);Assert.IsFalse(battle.IsBlocking);
    }
    [UnityTest] public IEnumerator AnimationWithoutWeaponContactCannotDealDamage()
    {
        yield return Begin();game.GetComponent<BadShiftWeaponContact>().enabled=false;
        battle.Attack();yield return new WaitForSeconds(.4f);Assert.AreEqual(100,battle.EnemyHealth);
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.AttackSound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name));
    }
    [UnityTest] public IEnumerator FallingFinishesBeforeResultMusicAndRematchStopsIt()
    {
        yield return Begin();
        for(int i=0;i<3;i++){Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();yield return new WaitForSeconds(.7f);}
        Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();yield return new WaitForSeconds(.25f);
        Assert.AreEqual(0,battle.EnemyHealth);Assert.IsTrue(battle.Falling);Assert.IsFalse(battle.Finished);Assert.Less(battle.FallProgress,.4f);
        Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.VictorySound.name));Assert.Greater(AudioSystem.Music.Volume,0);
        float deadline=Time.time+2;while(!battle.Finished&&Time.time<deadline)yield return null;
        Assert.AreEqual(BadShiftBattlePhase.Victory,battle.Phase);Assert.AreEqual(1,battle.FallProgress);Assert.AreEqual(0,AudioSystem.Music.Volume);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.VictorySound.name&&r.Outcome==PlaybackOutcome.Played));
        battle.Rematch();yield return null;Assert.Greater(AudioSystem.Music.Volume,0);Assert.AreEqual(100,battle.EnemyHealth);
    }
}
