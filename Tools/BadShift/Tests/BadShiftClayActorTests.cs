using System.Collections;
using System.IO;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftClayActorTests
{
    private BadShiftDemo demo;
    private BadShiftPoseProbe probe;
    [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.3f);demo=Object.FindFirstObjectByType<BadShiftDemo>();probe=demo.gameObject.AddComponent<BadShiftPoseProbe>();probe.Actor=demo.PlayerAnimator.transform;}
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Capture(string name,Transform actor){probe.Capture=()=>RenderPose(name,actor);}
    private void RenderPose(string name,Transform actor)
    {
        Camera camera=demo.Game.View;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float size=camera.orthographicSize;
        if(name!="reference-review"){camera.transform.position=actor.position+new Vector3(2,2.2f,-5);camera.transform.LookAt(actor.position+Vector3.up*1.3f);camera.orthographicSize=1.65f;}
        var target=new RenderTexture(640,640,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(name+".png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;Object.Destroy(image);Object.Destroy(target);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
    }
    [UnityTest] public IEnumerator ClayGeometryFollowsAnimatedBonesWithoutChangingCollision()
    {
        foreach(var actor in new[]{demo.PlayerAnimator,demo.DanceAnimator})
        {
            Assert.IsTrue(actor.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.enabled));
            var face=actor.GetComponent<BadShiftClayFace>();Assert.IsNotNull(face);Assert.AreEqual("head",face.Head.parent.name);
            Assert.AreEqual(2,face.Eyes.Length);Assert.IsEmpty(face.Head.GetComponentsInChildren<Collider>());
        }
        var body=demo.Game.GetComponent<CharacterController>();float height=body.height;var player=demo.PlayerAnimator.GetComponent<BadShiftClayFace>();
        Quaternion first=player.Head.localRotation;Capture("clay-idle-a",demo.PlayerAnimator.transform);yield return new WaitForSeconds(.5f);
        Assert.Greater(Quaternion.Angle(first,player.Head.localRotation),.1f);Assert.AreEqual(height,body.height);Capture("clay-idle-b",demo.PlayerAnimator.transform);
        demo.Game.RequestMusic(true);float until=Time.time+10;
        while(AudioSystem.Music.CurrentTrack.name!="Combat"&&Time.time<until)yield return null;
        Assert.AreEqual("Combat",AudioSystem.Music.CurrentTrack.name);yield return new WaitForSeconds(.6f);Capture("clay-dance",demo.DanceAnimator.transform);
    }
    [UnityTest] public IEnumerator FaceFreezesDuringVoiceAndPauseThenResumes()
    {
        var face=demo.PlayerAnimator.GetComponent<BadShiftClayFace>();demo.Game.Speak();yield return null;yield return null;
        Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);var held=face.Head.localRotation;yield return new WaitForSeconds(.15f);Assert.AreEqual(held,face.Head.localRotation);
        while(AudioSystem.VoiceOver.IsSpeaking)yield return null;
        yield return new WaitForSeconds(.3f);Assert.Greater(Quaternion.Angle(held,face.Head.localRotation),.01f);
        AudioSystem.SetPaused(true);Time.timeScale=0;held=face.Head.localRotation;yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual(held,face.Head.localRotation);Time.timeScale=1;AudioSystem.SetPaused(false);
    }
    [UnityTest] public IEnumerator PlantedFeetStayStillWhilePlayerTravels()
    {
        var game=demo.Game;game.enabled=false;var gait=game.ClayGait;Assert.IsNotNull(gait);
        Vector3 start=game.transform.position;int samples=0;float until=Time.time+1.4f;int frame=0;
        while(Time.time<until)
        {
            Vector3[] before=(Vector3[])gait.FootPositions.Clone();bool[] planted=(bool[])gait.Planted.Clone();
            game.MoveCharacter(Vector3.right,false,false,Time.deltaTime);yield return null;
            for(int foot=0;foot<2;foot++)if(planted[foot]&&gait.Planted[foot]&&gait.IsWalking){Assert.Less(Vector3.Distance(before[foot],gait.FootPositions[foot]),.015f);samples++;}
            if(++frame==20)Capture("clay-walk-a",demo.PlayerAnimator.transform);
            if(frame==35)Capture("clay-walk-b",demo.PlayerAnimator.transform);
        }
        Assert.Greater(samples,10);Assert.Greater(Vector3.Distance(start,game.transform.position),2);
        Assert.Greater(gait.Cycle,.5f);
        until=Time.time+.3f;while(Time.time<until){game.MoveCharacter(Vector3.zero,false,false,Time.deltaTime);yield return null;}
        Vector3 stopped=gait.FootPositions[0];yield return new WaitForSeconds(.2f);Assert.Less(Vector3.Distance(stopped,gait.FootPositions[0]),.001f);
        Capture("reference-review",demo.PlayerAnimator.transform);yield return null;
    }
    [UnityTest] public IEnumerator GaitDoesNotAccumulateHipOffsetAndFreezesOnPause()
    {
        var gait=demo.Game.ClayGait;var hips=demo.PlayerAnimator.GetComponentsInChildren<Transform>().First(t=>t.name=="hips");
        float initial=hips.localPosition.y;yield return new WaitForSeconds(1.5f);Assert.AreEqual(initial,hips.localPosition.y,.12f);
        AudioSystem.SetPaused(true);Time.timeScale=0;float cycle=gait.Cycle;Vector3 foot=gait.FootPositions[0];yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(cycle,gait.Cycle);Assert.AreEqual(foot,gait.FootPositions[0]);Time.timeScale=1;AudioSystem.SetPaused(false);
        yield return null;Assert.Greater(probe.HipHeight,.65f);
    }
}

[DefaultExecutionOrder(1000)]
public sealed class BadShiftPoseProbe : MonoBehaviour
{
    public System.Action Capture;
    public Transform Actor;
    public float HipHeight;
    private void LateUpdate()
    {
        HipHeight=Actor.GetComponentsInChildren<Transform>().First(t=>t.name=="hips").position.y-Actor.position.y;
        var capture=Capture;Capture=null;capture?.Invoke();
    }
}
