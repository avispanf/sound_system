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
    [UnitySetUp] public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.3f);demo=Object.FindFirstObjectByType<BadShiftDemo>();}
    [UnityTearDown] public IEnumerator Stop(){yield return PlaygroundTestCleanup.Clear();}
    private void Capture(string name,Transform actor)
    {
        Camera camera=demo.Game.View;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float size=camera.orthographicSize;
        camera.transform.position=actor.position+new Vector3(2,2.2f,-5);camera.transform.LookAt(actor.position+Vector3.up*1.2f);camera.orthographicSize=1.65f;
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
}
