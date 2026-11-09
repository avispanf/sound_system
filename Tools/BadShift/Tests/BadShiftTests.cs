using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftTests
{
    private BadShiftDemo demo;
    [UnitySetUp] public IEnumerator Load() { yield return SceneManager.LoadSceneAsync("RelayField"); yield return null;demo=Object.FindFirstObjectByType<BadShiftDemo>();Assert.IsNotNull(demo); }
    [UnityTearDown] public IEnumerator Stop(){AudioSystem.Shutdown();yield return null;}
    [UnityTest] public IEnumerator ClayMotionPreservesPhysicalBodyAndFixedCamera()
    {
        yield return new WaitForSeconds(.2f);
        Assert.IsNotNull(demo.GetComponent<BadShiftClayMotion>());
        Vector3 camera = demo.Game.View.transform.position;
        CharacterController c = demo.Game.GetComponent<CharacterController>();
        float height = c.height; float radius = c.radius;
        Vector3 visual = demo.PlayerAnimator.transform.localScale;
        yield return new WaitForSeconds(.15f);
        Assert.Greater(Vector3.Distance(visual, demo.PlayerAnimator.transform.localScale), .00001f);
        Assert.AreEqual(height, c.height); Assert.AreEqual(radius, c.radius);
        c.enabled = false; demo.Game.transform.position += Vector3.right * 2; c.enabled = true;
        yield return null;
        Assert.AreEqual(camera, demo.Game.View.transform.position);
        Assert.Less(Vector3.Distance(demo.Game.Listener.position, demo.Game.transform.position), 2);
    }
    [UnityTest] public IEnumerator ApplianceJigglesWithoutMovingItsPhysicalBodyAndPauses()
    {
        Assert.IsNotNull(demo.MicrowaveVisual);
        Bounds physical = demo.Microwave.GetComponent<Collider>().bounds;
        Quaternion rest = demo.MicrowaveVisual.localRotation;
        demo.PlayMicrowave(); yield return new WaitForSeconds(.1f);
        Assert.Greater(Quaternion.Angle(rest, demo.MicrowaveVisual.localRotation), .1f);
        Assert.AreEqual(physical, demo.Microwave.GetComponent<Collider>().bounds);
        AudioSystem.SetPaused(true); Time.timeScale = 0;
        Quaternion held = demo.MicrowaveVisual.localRotation;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(held, demo.MicrowaveVisual.localRotation);
        Time.timeScale = 1; AudioSystem.SetPaused(false);
        yield return new WaitForSeconds(.7f);
        Assert.Less(Quaternion.Angle(rest, demo.MicrowaveVisual.localRotation), .001f);
    }
    [UnityTest] public IEnumerator VoiceHoldsClayStaffWhileManagerRemainsExpressive()
    {
        demo.Game.Speak(); yield return null; yield return null;
        Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);
        Vector3 player = demo.PlayerAnimator.transform.localScale;
        Vector3 staff = demo.DanceAnimator.transform.localScale;
        Quaternion manager = demo.Boss.localRotation;
        yield return new WaitForSeconds(.12f);
        Assert.AreEqual(player, demo.PlayerAnimator.transform.localScale);
        Assert.AreEqual(staff, demo.DanceAnimator.transform.localScale);
        Assert.Greater(Quaternion.Angle(manager, demo.Boss.localRotation), .1f);
    }
    [UnityTest] public IEnumerator ShuffleBagShowsActualDistinctSelections()
    {
        var clips=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<demo.MicrowaveBag.Clips.Length;i++){demo.PlayMicrowave();clips.Add(demo.LastAnswer);yield return null;}
        Assert.AreEqual(demo.MicrowaveBag.Clips.Length,clips.Count,string.Join(" | ",clips)+" / "+string.Join(" | ",AudioSystem.Debugger.Records.Where(r=>r.EventName==demo.MicrowaveBag.name).Select(r=>r.ClipName+":"+r.DescribeOutcome())));
        demo.ToggleSelection();demo.PlayMicrowave();Assert.IsTrue(demo.MicrowaveRandom.Clips.Any(c=>c.name==demo.LastAnswer));
    }
    [UnityTest] public IEnumerator ProcessingUsesSameClipAndRealFilters()
    {
        AudioClip clip=demo.RoomSource.clip;
        for(int i=0;i<3;i++){demo.SetEffect(i);Assert.AreEqual(i==1,demo.Echo.enabled);Assert.AreEqual(i==2,demo.Reverb.enabled);demo.PlayRoom();yield return null;Assert.AreSame(clip,demo.RoomSource.clip);Assert.IsTrue(demo.RoomSource.isPlaying);}
    }
    [UnityTest] public IEnumerator StaffReactionWaitsForMusicAndFreezesDuringVoice()
    {
        Assert.IsFalse(demo.Game.transform.Find("Player body").GetComponent<Renderer>().enabled);
        Assert.IsNotNull(demo.PlayerAnimator.runtimeAnimatorController);
        demo.Game.RequestMusic(true);yield return null;
        if(AudioSystem.Music.IsTransitionPending)Assert.IsTrue(demo.DanceAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
        float until=Time.realtimeSinceStartup+6;
        while(AudioSystem.Music.IsTransitionPending && Time.realtimeSinceStartup<until)yield return null;
        yield return null;Assert.AreEqual("Combat",AudioSystem.Music.CurrentTrack.name);Assert.AreEqual(1,demo.DanceAnimator.speed);
        demo.Game.RequestMusic(false);yield return null;
        if(AudioSystem.Music.IsTransitionPending)Assert.AreEqual(1,demo.DanceAnimator.speed);
        demo.Game.Speak();yield return null;Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);Assert.AreEqual(0,demo.DanceAnimator.speed);
    }
    [UnityTest] public IEnumerator EveryIngredientAndExitHasAWalkableRoute()
    {
        yield return new WaitForSeconds(.2f);
        var reachable=new System.Collections.Generic.HashSet<Vector2Int>();var queue=new System.Collections.Generic.Queue<Vector2Int>();
        Vector2Int start=new Vector2Int(0,-8);queue.Enqueue(start);reachable.Add(start);int mask=~(1<<2);float top=demo.Game.GetComponent<CharacterController>().height-.35f;
        Vector2Int[] directions={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        while(queue.Count>0)
        {
            Vector2Int current=queue.Dequeue();Vector3 a=new Vector3(current.x,.05f,current.y);
            foreach(Vector2Int direction in directions)
            {
                Vector2Int next=current+direction;if(next.x < -11 || next.x > 11 || next.y < -9 || next.y > 9 || reachable.Contains(next))continue;
                Vector3 b=new Vector3(next.x,.05f,next.y);
                if(Physics.CheckCapsule(b+Vector3.up*.35f,b+Vector3.up*top,.3f,mask,QueryTriggerInteraction.Ignore))continue;
                if(Physics.CapsuleCast(a+Vector3.up*.35f,a+Vector3.up*top,.3f,(b-a).normalized,1,mask,QueryTriggerInteraction.Ignore))continue;
                reachable.Add(next);queue.Enqueue(next);
            }
        }
        foreach(PlaygroundPickup pickup in demo.Game.Pickups)Assert.IsTrue(reachable.Any(n=>Vector3.Distance(new Vector3(n.x,.05f,n.y),pickup.transform.position)<1.95f),pickup.name+" is unreachable");
        Assert.IsTrue(reachable.Any(n=>Vector3.Distance(new Vector3(n.x,.05f,n.y),demo.Game.Exit.position)<2.45f),"Exit is unreachable");
    }
    [UnityTest] public IEnumerator FurnitureStopsARealCharacterSweep()
    {
        yield return new WaitForSeconds(.2f);
        MeshCollider[] colliders=demo.Fridge.GetComponentsInChildren<MeshCollider>();Assert.IsNotEmpty(colliders);
        Bounds bounds=colliders[0].bounds;foreach(var c in colliders)bounds.Encapsulate(c.bounds);
        Assert.Greater(bounds.size.y, 3f, "Refrigerator must remain upright after model rotation");
        CharacterController controller=demo.Game.GetComponent<CharacterController>();demo.Game.enabled=false;controller.enabled=false;
        demo.Game.transform.position=new Vector3(bounds.center.x,.05f,bounds.min.z-2);controller.enabled=true;Physics.SyncTransforms();
        for(int i=0;i<30;i++){controller.Move(Vector3.forward*.2f+Vector3.down*.02f);yield return null;}
        Assert.Less(demo.Game.transform.position.z,bounds.center.z,"Player passed through the refrigerator");
    }
    [UnityTest] public IEnumerator AllCharactersHaveLiveIdleAndVoiceReaction()
    {
        yield return new WaitForSeconds(.3f);Assert.AreEqual(1,demo.DanceAnimator.speed);Assert.IsTrue(demo.DanceAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
        Quaternion before=demo.Boss.localRotation;demo.Game.Speak();yield return new WaitForSeconds(.2f);
        Assert.Greater(Quaternion.Angle(before,demo.Boss.localRotation),.1f);Assert.IsTrue(demo.Game.FreezeForVoice);Assert.AreEqual(0,demo.PlayerAnimator.speed);
    }
    [UnityTest] public IEnumerator FridgeStaysFixedAndMeasuresPlayerListener()
    {
        Vector3 fixedPosition=demo.Fridge.position;float before=demo.ListenerDistance;
        CharacterController c=demo.Game.GetComponent<CharacterController>();c.enabled=false;demo.Game.transform.position=demo.Fridge.position+Vector3.back*2;c.enabled=true;yield return null;
        Assert.AreEqual(fixedPosition,demo.Fridge.position);Assert.Less(demo.ListenerDistance,before);
        Assert.AreEqual(Vector3.Distance(demo.Game.Listener.position,fixedPosition),demo.ListenerDistance,.001f);
    }
}
