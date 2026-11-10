using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftComboTests
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
    [UnityTest] public IEnumerator ThreeContactsChainDistinctSwingsWithoutExtraDamage()
    {
        for(int i=1;i<=3;i++)
        {
            battle.Attack();Assert.AreEqual(i,battle.PlayerStrikeStep);yield return new WaitForSeconds(.06f);
            string name="combo-windup-"+i;probe.Capture=()=>Capture(name);yield return null;
            yield return new WaitForSeconds(.25f);Assert.AreEqual(100-25*i,battle.EnemyHealth);Assert.AreEqual(i,battle.ComboHits);
            yield return new WaitForSeconds(.39f);
        }
        Assert.AreEqual(3,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.HitSound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.AreEqual(100,battle.PlayerHealth);Assert.IsTrue(game.IsGrounded);Assert.IsTrue(battle.EnemyTaunt.Contains("soup"));
    }
    [UnityTest] public IEnumerator LateClickQueuesOnlyOneFollowUpAndGuardCancelsIt()
    {
        battle.Attack();yield return new WaitForSeconds(.46f);Assert.AreEqual(1,battle.ComboHits);
        for(int i=0;i<8;i++)battle.Attack();Assert.IsTrue(battle.AttackQueued);Assert.AreEqual(1,battle.PlayerStrikeStep);
        yield return new WaitForSeconds(.65f);Assert.AreEqual(2,battle.PlayerStrikeStep);Assert.AreEqual(50,battle.EnemyHealth);Assert.IsFalse(battle.AttackQueued);
        battle.Attack();Assert.IsTrue(battle.AttackQueued);battle.SetBlocking(true);Assert.IsFalse(battle.AttackQueued);Assert.AreEqual(0,battle.ComboHits);
        yield return new WaitForSeconds(.5f);Assert.AreEqual(2,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.AttackSound.name&&r.Outcome==PlaybackOutcome.Played));
    }
    [UnityTest] public IEnumerator MissExpiryAndRematchClearTheChain()
    {
        var contact=game.GetComponent<BadShiftWeaponContact>();contact.enabled=false;battle.Attack();yield return new WaitForSeconds(.7f);
        Assert.AreEqual(0,battle.ComboHits);Assert.IsFalse(battle.AttackQueued);Assert.AreEqual(100,battle.EnemyHealth);
        contact.enabled=true;battle.Attack();Assert.AreEqual(1,battle.PlayerStrikeStep);yield return new WaitForSeconds(.4f);Assert.AreEqual(1,battle.ComboHits);
        Place(battle.ArenaCenter+Vector3.left*4);yield return new WaitForSeconds(1.1f);Assert.AreEqual(0,battle.ComboHits);
        battle.Rematch();yield return new WaitForSeconds(1.3f);battle.Attack();Assert.AreEqual(1,battle.PlayerStrikeStep);
        yield return new WaitForSeconds(.46f);battle.Attack();Assert.IsTrue(battle.AttackQueued);battle.Rematch();
        Assert.IsFalse(battle.AttackQueued);Assert.AreEqual(0,battle.ComboHits);Assert.AreEqual(0,battle.PlayerStrikeStep);Assert.AreEqual(0,battle.BlockFeedback);
    }
    [UnityTest] public IEnumerator SuccessfulBlockReboundsEnemyAndAllowsCounterWithoutDamage()
    {
        battle.SetBlocking(true);float deadline=Time.time+3;
        while(battle.BlockFeedback==0&&Time.time<deadline)yield return null;
        Assert.Greater(battle.BlockFeedback,0);Assert.AreEqual(100,battle.PlayerHealth);Assert.AreEqual(100,battle.EnemyHealth);Assert.AreEqual(0,battle.EnemySwingUntil);
        Vector3 before=battle.EnemyBody.transform.position;yield return new WaitForSeconds(.18f);Assert.Greater(Vector3.Distance(before,battle.EnemyBody.transform.position),.1f);
        probe.Capture=()=>Capture("combo-block-rebound");yield return null;
        AudioSystem.SetPaused(true);Time.timeScale=0;float feedback=battle.BlockFeedback;Vector3 held=battle.EnemyBody.transform.position;
        yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(feedback,battle.BlockFeedback);Assert.AreEqual(held,battle.EnemyBody.transform.position);
        Time.timeScale=1;AudioSystem.SetPaused(false);battle.SetBlocking(false);
        Place(battle.EnemyBody.transform.position+Vector3.left*1.4f);battle.Attack();yield return new WaitForSeconds(.3f);
        Assert.AreEqual(75,battle.EnemyHealth);Assert.AreEqual(100,battle.PlayerHealth);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.BlockSound.name&&r.Outcome==PlaybackOutcome.Played));
    }
}
