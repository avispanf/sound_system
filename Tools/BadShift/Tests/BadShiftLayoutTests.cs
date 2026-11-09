using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftLayoutTests
{
    private AudioMWPlayground game;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.4f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();
    }
    [UnityTearDown] public IEnumerator Cleanup(){yield return PlaygroundTestCleanup.Clear();}
    private void Place(Vector3 position)
    {
        CharacterController body=game.GetComponent<CharacterController>();body.enabled=false;game.transform.position=position;body.enabled=true;Physics.SyncTransforms();
    }
    [UnityTest] public IEnumerator ThreeDiningRowsAreEvenAndRemainClearOfWalls()
    {
        Rigidbody[] tables=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name=="Pushable dining table").OrderBy(b=>b.position.z).ToArray();
        Assert.AreEqual(3,tables.Length);
        for(int i=0;i<tables.Length;i++){Assert.AreEqual(-8,tables[i].position.x,.03f);Assert.AreEqual(-4.8f+i*3.2f,tables[i].position.z,.03f);}
        Rigidbody[] chairs=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name=="Coral cafe chair").ToArray();
        Assert.AreEqual(6,chairs.Length);foreach(Rigidbody chair in chairs)Assert.Greater(chair.GetComponentsInChildren<Collider>().Min(c=>c.bounds.min.x),-11);
        Assert.AreEqual(6,BadShiftImpactProp.InitialBreakableCount);yield return null;
    }
    [UnityTest] public IEnumerator MicrowaveRestsAboveTheCounterAndBackEdgesAlign()
    {
        BadShiftDemo demo=game.GetComponent<BadShiftDemo>();
        Bounds microwave=demo.MicrowaveVisual.GetComponent<Renderer>().bounds;
        Bounds counter=GameObject.Find("Kitchen counter top").GetComponent<Renderer>().bounds;
        Assert.Greater(counter.min.x,4.3f);
        Assert.That(microwave.min.y-counter.max.y,Is.InRange(.035f,.05f));
        Assert.AreEqual(counter.center.x,microwave.center.x,.01f);Assert.AreEqual(counter.center.z,microwave.center.z,.01f);
        Bounds fridge=demo.Fridge.GetComponent<Renderer>().bounds;Assert.AreEqual(counter.max.z,fridge.max.z,.02f);
        yield return null;
    }
    [UnityTest] public IEnumerator RefrigeratorDoorsOperateIndependentlyAndPreserveTheHousing()
    {
        BadShiftFridge fridge=game.FridgeDoors;Assert.IsNotNull(fridge);Vector3 housing=game.GetComponent<BadShiftDemo>().Fridge.position;
        Place(new Vector3(7,.05f,5.25f));game.Interact();yield return new WaitForSeconds(.6f);
        Assert.IsTrue(fridge.Lower.IsOpen);Assert.IsFalse(fridge.Lower.IsMoving);Assert.IsFalse(fridge.Upper.IsOpen);
        Assert.Greater(Quaternion.Angle(fridge.Lower.transform.rotation,Quaternion.identity),89);
        Assert.IsTrue(fridge.ToggleUpperNear(game.transform.position));yield return new WaitForSeconds(.6f);
        Assert.IsTrue(fridge.Upper.IsOpen);Assert.IsFalse(fridge.Upper.IsMoving);Assert.Greater(Quaternion.Angle(fridge.Upper.transform.rotation,Quaternion.identity),89);
        Assert.Less(fridge.Lower.Leaf.bounds.max.x,11.7f);Assert.Less(fridge.Upper.Leaf.bounds.max.x,11.7f);
        game.Interact();fridge.ToggleUpperNear(game.transform.position);yield return new WaitForSeconds(.6f);
        Assert.IsFalse(fridge.Lower.IsMoving);Assert.IsFalse(fridge.Upper.IsMoving);Assert.IsFalse(fridge.Lower.IsOpen);Assert.IsFalse(fridge.Upper.IsOpen);
        Assert.AreEqual(housing,game.GetComponent<BadShiftDemo>().Fridge.position);
        Place(new Vector3(0,.05f,-8));Assert.IsFalse(fridge.ToggleLowerNear(game.transform.position));Assert.IsFalse(fridge.ToggleUpperNear(game.transform.position));
    }
    [UnityTest] public IEnumerator BathroomFixturesLeaveTheDoorSweepAndEntryClear()
    {
        Assert.IsNotNull(GameObject.Find("Toilet bowl"));Assert.IsNotNull(GameObject.Find("Sink basin"));
        Place(new Vector3(-.9f,.05f,4.8f));game.Interact();yield return new WaitForSeconds(.6f);Assert.IsFalse(game.Door.IsMoving);
        Assert.IsFalse(game.Door.IsBlocked);
        Place(new Vector3(-3,.05f,5));game.enabled=false;
        float until=Time.time+.65f;while(Time.time<until){game.MoveCharacter(Vector3.forward,false,false,Time.deltaTime);yield return null;}
        Assert.Greater(game.transform.position.z,6.4f);Assert.Less(game.transform.position.z,7.6f);
    }
    [UnityTest] public IEnumerator PreparationTableAndLooseDishesRemainStable()
    {
        var table=GameObject.Find("Kitchen preparation table").GetComponent<Rigidbody>();
        Vector3 start=table.position;yield return new WaitForSeconds(1);
        Assert.Less(Vector3.Distance(start,table.position),.08f);
        Assert.Less(Quaternion.Angle(table.rotation,Quaternion.identity),2);
        foreach(string name in new[]{"Prep bread","Prep bowl"})
        {
            var item=GameObject.Find(name);Assert.IsNotNull(item.GetComponent<BadShiftImpactProp>());
            Assert.Greater(item.transform.position.y,1.3f);Assert.Less(item.GetComponent<Rigidbody>().linearVelocity.magnitude,.1f);
        }
    }
}
