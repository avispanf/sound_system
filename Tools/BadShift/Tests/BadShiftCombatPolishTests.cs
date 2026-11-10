using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftCombatPolishTests
{
    private AudioMWPlayground game;
    private BadShiftBattle battle;
    private BadShiftPoseProbe probe;
    private Transform Player=>battle.Life.Demo.PlayerAnimator.transform;
    private Transform Enemy=>battle.Life.Demo.DanceAnimator.transform;
    private static Transform Bone(Transform actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();battle=game.Life.Battle;game.GetComponent<BadShiftBattleInput>().enabled=false;
        probe=game.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=Player;
        Place(battle.ArenaCenter+Vector3.left*1.5f);game.Interact();yield return new WaitForSeconds(1.3f);
    }
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 p){var body=game.GetComponent<CharacterController>();body.enabled=false;game.transform.position=p;body.enabled=true;Physics.SyncTransforms();}
    private void Capture(string name)
    {
        Camera camera=game.View;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float size=camera.orthographicSize;
        Vector3 center=(game.transform.position+battle.EnemyBody.transform.position)*.5f+Vector3.up*1.2f;
        camera.transform.position=center+new Vector3(-3,2,-7);camera.transform.LookAt(center);camera.orthographicSize=2.7f;
        var target=new RenderTexture(1200,800,24);var previous=RenderTexture.active;var old=camera.targetTexture;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=previous;Object.Destroy(image);Object.Destroy(target);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
    }
    private static float GripDistance(Transform hand,Transform stick)
    {
        Vector3 a=stick.TransformPoint(Vector3.down*.5f),b=stick.TransformPoint(Vector3.up*.5f),line=b-a;
        return Vector3.Distance(hand.position,a+line*Mathf.Clamp01(Vector3.Dot(hand.position-a,line)/line.sqrMagnitude));
    }
    [UnityTest] public IEnumerator GuardHasTwoHandedGripAndClearsOnExit()
    {
        battle.SetBlocking(true);yield return null;
        bool captured=false;
        probe.Capture=()=>
        {
            var stick=battle.Life.PlayerStick.transform;
            Assert.Less(GripDistance(Bone(Player,"hand.r"),stick),.10f);
            Assert.Less(GripDistance(Bone(Player,"hand.l"),stick),.19f);
            Assert.Greater(Vector3.Dot(stick.up,Vector3.up),.7f);
            Assert.IsTrue(game.transform.Find("Guard accent").GetComponent<LineRenderer>().enabled);
            Capture("combat-guard");captured=true;
        };
        yield return null;Assert.IsTrue(captured);Assert.IsTrue(battle.Status.Contains("BLOCKING"));
        battle.ReturnToCafe();yield return null;yield return null;
        Assert.IsFalse(game.transform.Find("Guard accent").GetComponent<LineRenderer>().enabled);
        Assert.IsFalse(game.GetComponentsInChildren<LineRenderer>().Where(l=>l.name.StartsWith("Bonk accent")||l.name.StartsWith("Breadstick swish")).Any(l=>l.enabled));
    }
    [UnityTest] public IEnumerator ExpressiveSwingKeepsGripAndProducesOneContact()
    {
        AudioSystem.Debugger.Clear();battle.Attack();float until=Time.time+.35f;int samples=0;bool anticipation=false,impact=false;
        while(Time.time<until)
        {
            probe.Capture=()=>
            {
                Assert.Less(GripDistance(Bone(Player,"hand.r"),battle.Life.PlayerStick.transform),.10f);samples++;
                if(!anticipation){Capture("combat-windup");anticipation=true;}
                if(!impact&&battle.EnemyHealth<100){Capture("combat-bonk");impact=true;}
            };
            yield return null;
        }
        Assert.Greater(samples,2);Assert.IsTrue(impact);Assert.AreEqual(75,battle.EnemyHealth);
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(r=>r.EventName==battle.HitSound.name&&r.Outcome==PlaybackOutcome.Played));
    }
    [UnityTest] public IEnumerator ClayFallSettlesBeforeResultAndRestoresOnRematch()
    {
        Transform rig=Enemy.Find("Rig");Vector3 rest=rig.localScale;
        for(int i=0;i<3;i++){Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();yield return new WaitForSeconds(.7f);}
        Place(battle.EnemyBody.transform.position+Vector3.left*1.5f);battle.Attack();
        float deadline=Time.time+1;while(!battle.Falling&&Time.time<deadline)yield return null;Assert.IsTrue(battle.Falling);
        float minScale=float.MaxValue;bool start=false,flop=false,settle=false;
        while(!battle.Finished)
        {
            probe.Capture=()=>
            {
                minScale=Mathf.Min(minScale,rig.localScale.y/rest.y);Assert.IsFalse(float.IsNaN(rig.localScale.x));
                Assert.GreaterOrEqual(Enemy.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Min(r=>r.bounds.min.y),battle.EnemyBody.transform.position.y+.015f);
                if(!start&&battle.FallProgress>.12f){Capture("combat-fall-recoil");start=true;}
                if(!flop&&battle.FallProgress>.5f){Capture("combat-fall-flop");flop=true;}
                if(!settle&&battle.FallProgress>.8f){Capture("combat-fall-settle");settle=true;}
            };
            yield return null;
        }
        Assert.Less(minScale,.9f);Assert.IsTrue(start&&flop&&settle);Assert.AreEqual(1,battle.FallProgress);
        Quaternion final=Quaternion.identity;probe.Capture=()=>final=rig.localRotation;yield return null;yield return new WaitForSeconds(.3f);
        probe.Capture=()=>{Assert.Less(Quaternion.Angle(final,rig.localRotation),.01f);Capture("combat-fall-rest");};yield return null;
        battle.Rematch();yield return null;yield return null;
        probe.Capture=()=>{Assert.Less(Vector3.Distance(rest,rig.localScale),.01f);Assert.Less(Quaternion.Angle(Quaternion.identity,rig.localRotation),1);};yield return null;
    }
}
