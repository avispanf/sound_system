using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftPropsTests
{
    private AudioMWPlayground game;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField"); yield return new WaitForSeconds(.4f);
        game = Object.FindFirstObjectByType<AudioMWPlayground>();
    }
    [UnityTearDown] public IEnumerator Cleanup() { yield return PlaygroundTestCleanup.Clear(); }
    private void Place(Vector3 point)
    {
        CharacterController controller=game.GetComponent<CharacterController>();controller.enabled=false;game.transform.position=point;controller.enabled=true;Physics.SyncTransforms();
    }
    [UnityTest] public IEnumerator WalkingIntoTomatoKicksItIntoTheAir()
    {
        BadShiftKickable ball=Object.FindObjectsByType<BadShiftKickable>(FindObjectsSortMode.None).First(b=>b.GetComponent<PlaygroundPickup>()!=null);
        Rigidbody body=ball.GetComponent<Rigidbody>();
        body.position=new Vector3(3,.7f,-5);body.linearVelocity=Vector3.zero;
        yield return new WaitForSeconds(.8f);
        Vector3 before=body.position;Place(new Vector3(before.x,.05f,before.z-1.2f));game.enabled=false;
        float peak=before.y;float deadline=Time.time+1;
        while(Time.time<deadline){game.MoveCharacter(Vector3.forward,false,false,Time.deltaTime);peak=Mathf.Max(peak,body.position.y);yield return null;}
        Assert.Greater(body.position.z,before.z+.6f);Assert.Greater(peak,before.y+.15f);
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName==ball.GetComponent<BadShiftImpactProp>().ImpactSound.name));
    }
    [UnityTest] public IEnumerator PicnicAndCrateCanTipUnderAnImpulse()
    {
        foreach(string name in new[]{"Pushable picnic assembly","Tip-over tomato crate"})
        {
            Rigidbody body=GameObject.Find(name).GetComponent<Rigidbody>();
            Assert.IsFalse(body.isKinematic);Assert.Greater(body.GetComponentsInChildren<Collider>().Length,3);
            body.AddForceAtPosition(Vector3.right*body.mass*4,body.worldCenterOfMass+Vector3.up*.8f,ForceMode.Impulse);
        }
        string[] names={"Pushable picnic assembly","Tip-over tomato crate"};float[] peaks=new float[2];
        float deadline=Time.time+1.2f;
        while(Time.time<deadline)
        {
            for(int i=0;i<names.Length;i++)peaks[i]=Mathf.Max(peaks[i],Vector3.Angle(GameObject.Find(names[i]).transform.up,Vector3.up));
            yield return null;
        }
        for(int i=0;i<names.Length;i++)Assert.Greater(peaks[i],20,names[i]);
        Assert.AreEqual(4,Object.FindObjectsByType<BadShiftKickable>(FindObjectsSortMode.None).Count(b=>b.name=="Loose crate tomato"));
    }
    [UnityTest] public IEnumerator NearbyInteractionOpensAndClosesTheDoor()
    {
        BadShiftDoor door=game.Door;Assert.IsNotNull(door);
        Place(new Vector3(-3,.05f,4.8f));game.Interact();Assert.IsTrue(door.IsOpen);
        yield return new WaitForSeconds(1.4f);Assert.IsFalse(door.IsMoving);Assert.Greater(Quaternion.Angle(door.transform.rotation,Quaternion.identity),90);
        game.Interact();Assert.IsFalse(door.IsOpen);
        yield return new WaitForSeconds(1.4f);Assert.IsFalse(door.IsMoving);Assert.Less(Quaternion.Angle(door.transform.rotation,Quaternion.identity),1);
    }
    [UnityTest] public IEnumerator UntouchedPropsSettleWithoutDrifting()
    {
        yield return new WaitForSeconds(3);
        Rigidbody[] bodies=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>!b.isKinematic).ToArray();
        foreach(Rigidbody jar in bodies.Where(b=>b.name=="jar_A_medium"))Assert.Greater(jar.position.y,3.3f,"Shelf must support its jars");
        Vector3[] positions=bodies.Select(b=>b.position).ToArray();
        yield return new WaitForSeconds(1);
        for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null)Assert.Less(Vector3.Distance(positions[i],bodies[i].position),.1f,bodies[i].name);
    }
    [UnityTest] public IEnumerator DoorStopsBeforeSweepingIntoThePlayer()
    {
        BadShiftDoor door=game.Door;Place(new Vector3(-3.1f,.05f,6.5f));game.enabled=false;
        door.SetOpen(true);yield return new WaitForSeconds(1.4f);
        Assert.IsTrue(door.IsMoving);Assert.Less(Quaternion.Angle(door.transform.rotation,Quaternion.identity),80);
        Place(new Vector3(2,.05f,-5));yield return new WaitForSeconds(1.4f);Assert.IsFalse(door.IsMoving);
    }
}
