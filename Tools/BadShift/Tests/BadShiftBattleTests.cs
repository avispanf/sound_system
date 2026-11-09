using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftBattleTests
{
    private AudioMWPlayground game;
    private BadShiftBattle battle;
    private BadShiftPoseProbe probe;
    [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.5f);game=Object.FindFirstObjectByType<AudioMWPlayground>();battle=game.Life.Battle;probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=battle.Life.Demo.PlayerAnimator.transform;}
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var c=game.GetComponent<CharacterController>();c.enabled=false;game.transform.position=p;c.enabled=true;Physics.SyncTransforms();}
    private IEnumerator StartBattle(){Place(battle.ArenaCenter+Vector3.left*1.5f);game.Interact();yield return new WaitForSeconds(1.3f);Assert.AreEqual(BadShiftBattlePhase.Fighting,battle.Phase);}
    [UnityTest] public IEnumerator InvitationAndIntroDoNotDamageTheDancer()
    {
        Place(battle.ArenaCenter+Vector3.left*1.5f);yield return null;
        Assert.IsTrue(battle.CanInvite);Assert.IsTrue(battle.Life.Demo.EmployeeDancing);Assert.Greater(Vector3.Distance(battle.EnemyBody.transform.position,battle.Life.Demo.Room.position),8);
        game.Interact();game.Interact();Assert.AreEqual(BadShiftBattlePhase.Intro,battle.Phase);Assert.AreEqual(100,battle.EnemyHealth);Assert.IsFalse(battle.Life.Demo.EmployeeDancing);
        yield return new WaitForSeconds(.5f);Assert.AreEqual(100,battle.PlayerHealth);Assert.AreEqual(100,battle.EnemyHealth);
    }
    [UnityTest] public IEnumerator HitHasSeparateAudioFlashAndCollisionAwareKnockback()
    {
        yield return StartBattle();var enemy=battle.EnemyBody.transform;Vector3 before=enemy.position;
        battle.Attack();Assert.AreEqual(100,battle.EnemyHealth);yield return new WaitForSeconds(.24f);
        Assert.AreEqual(75,battle.EnemyHealth);Assert.Greater(battle.EnemyFlashUntil,Time.time);
        Assert.Greater(Vector3.Distance(before,enemy.position),.04f);
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.AttackSound.name&&r.Outcome==PlaybackOutcome.Played));
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==battle.HitSound.name&&r.Outcome==PlaybackOutcome.Played));
        battle.Attack();yield return new WaitForSeconds(.12f);Assert.AreEqual(75,battle.EnemyHealth);
    }
    [UnityTest] public IEnumerator MovingOutOfTelegraphAvoidsDamage()
    {
        yield return StartBattle();float deadline=Time.time+2;while(!battle.EnemyWindingUp&&Time.time<deadline)yield return null;Assert.IsTrue(battle.EnemyWindingUp);
        Place(battle.ArenaCenter+new Vector3(-3.5f,0,-2));yield return new WaitForSeconds(.8f);Assert.AreEqual(100,battle.PlayerHealth);
    }
    [UnityTest] public IEnumerator WallsBlockStrikes()
    {
        yield return StartBattle();var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(game.transform.position+battle.EnemyBody.transform.position)*.5f+Vector3.up;
        wall.transform.localScale=new Vector3(.15f,2,2);Physics.SyncTransforms();battle.Attack();yield return new WaitForSeconds(.25f);Assert.AreEqual(100,battle.EnemyHealth);Object.Destroy(wall);
    }
    [UnityTest] public IEnumerator PauseFreezesFightAndReturnRestoresPresentation()
    {
        var fx=game.GetComponent<BadShiftBattlePresentation>();yield return StartBattle();Assert.Greater(fx.Weight,.95f);
        AudioSystem.SetPaused(true);Time.timeScale=0;int health=battle.PlayerHealth;Vector3 enemy=battle.EnemyBody.transform.position;
        yield return new WaitForSecondsRealtime(.7f);Assert.AreEqual(health,battle.PlayerHealth);Assert.AreEqual(enemy,battle.EnemyBody.transform.position);
        AudioSystem.SetPaused(false);Time.timeScale=1;battle.ReturnToCafe();yield return new WaitForSeconds(.8f);
        Assert.AreEqual(0,fx.Weight,.001f);Assert.IsFalse(battle.Active);Assert.IsFalse(fx.Smoke[0].emission.enabled);Assert.AreEqual(0,fx.RedLight.intensity,.001f);
    }
    [UnityTest] public IEnumerator KnockbackPreservesGroundingWithoutFalseLandingSounds()
    {
        yield return StartBattle();AudioSystem.Debugger.Clear();float deadline=Time.time+3;
        while(battle.PlayerHealth==100&&Time.time<deadline)yield return null;
        Assert.AreEqual(80,battle.PlayerHealth);yield return new WaitForSeconds(.4f);
        Assert.IsTrue(game.IsGrounded);Assert.IsFalse(AudioSystem.Debugger.Records.Any(r=>r.EventName==game.Landing.name));
        Assert.IsFalse(float.IsNaN(battle.Life.Demo.PlayerAnimator.transform.localScale.x));
    }
    private void Capture(string name)
    {
        probe.Capture=()=>
        {
        var camera=game.View;var target=new RenderTexture(1440,900,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(name,image.EncodeToPNG());
        camera.targetTexture=oldTarget;RenderTexture.active=previous;Object.Destroy(target);Object.Destroy(image);
        };
    }
    private IEnumerator Advance(float seconds,Vector3 direction)
    {
        float until=Time.time+seconds;while(Time.time<until){game.MoveCharacter(direction,false,false,Time.deltaTime);yield return null;}
    }
    [UnityTest] public IEnumerator CaptureBattlePresentation()
    {
        Place(battle.ArenaCenter+Vector3.left*1.5f);game.OrbitCamera.FocusPlayer();yield return new WaitForSeconds(.6f);
        Capture("battle-invitation.png");yield return new WaitForSeconds(.2f);
        game.Interact();yield return new WaitForSeconds(.4f);Capture("battle-intro.png");yield return new WaitForSeconds(1.1f);
        game.enabled=false;
        for(int i=0;i<4;i++)
        {
            float deadline=Time.time+3;
            while(Vector3.ProjectOnPlane(battle.EnemyBody.transform.position-game.transform.position,Vector3.up).magnitude>1.65f&&Time.time<deadline)
            {
                game.MoveCharacter((battle.EnemyBody.transform.position-game.transform.position).normalized,false,false,Time.deltaTime);yield return null;
            }
            battle.Attack();yield return Advance(.22f,Vector3.zero);if(i==0)Capture("battle-hit.png");yield return Advance(.5f,Vector3.zero);
        }
        yield return Advance(1.2f,Vector3.zero);Assert.AreEqual(BadShiftBattlePhase.Victory,battle.Phase);Capture("battle-victory.png");yield return Advance(.2f,Vector3.zero);game.enabled=true;
    }
}
