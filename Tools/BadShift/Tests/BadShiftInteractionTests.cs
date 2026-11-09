using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftInteractionTests
{
    private AudioMWPlayground game;
    private Keyboard keyboard;
    private bool addedKeyboard;
    [UnitySetUp] public IEnumerator Load()
    {
        keyboard=Keyboard.current;
        if(keyboard==null){keyboard=InputSystem.AddDevice<Keyboard>();addedKeyboard=true;}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.3f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();
    }
    [UnityTearDown] public IEnumerator Stop()
    {
        yield return PlaygroundTestCleanup.Clear();
        if(addedKeyboard)InputSystem.RemoveDevice(keyboard);else InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        addedKeyboard=false;yield return null;
    }
    private void PlacePlayer(Vector3 position)
    {
        CharacterController controller=game.GetComponent<CharacterController>();controller.enabled=false;game.transform.position=position;controller.enabled=true;Physics.SyncTransforms();
    }
    [UnityTest] public IEnumerator OrbitAndZoomKeepTheAudioListenerOnThePlayer()
    {
        Vector3 ears=game.Listener.position;Quaternion rotation=game.View.transform.rotation;float size=game.View.orthographicSize;
        game.OrbitCamera.Orbit(90,15);game.OrbitCamera.Zoom(4);yield return new WaitForSeconds(.1f);
        Assert.Greater(Quaternion.Angle(rotation,game.View.transform.rotation),50);
        Assert.Less(game.View.orthographicSize,size-1);Assert.Less(Vector3.Distance(ears,game.Listener.position),.05f);
        game.OrbitCamera.Orbit(0,500);game.OrbitCamera.Zoom(100);Assert.AreEqual(75,game.OrbitCamera.Pitch);Assert.AreEqual(3.5f,game.OrbitCamera.Size);
        game.OrbitCamera.Home();Assert.AreEqual(12.8f,game.OrbitCamera.Size);Assert.IsFalse(game.OrbitCamera.FollowingPlayer);
    }
    [UnityTest] public IEnumerator ARealJumpHasABoundedHeightAndShortFlight()
    {
        float start=game.transform.position.y;float peak=start;float airborneAt=-1;float landedAt=-1;
        game.enabled=false;bool jump=true;
        float until=Time.time+1.2f;
        while(Time.time<until)
        {
            game.MoveCharacter(Vector3.zero,jump,false,Time.deltaTime);jump=false;
            peak=Mathf.Max(peak,game.transform.position.y);
            if(!game.IsGrounded && airborneAt<0)airborneAt=Time.time;
            if(airborneAt>=0 && game.IsGrounded){landedAt=Time.time;break;}
            yield return null;
        }
        Assert.Greater(peak-start,.4f);Assert.Less(peak-start,.7f);Assert.Greater(landedAt,airborneAt);Assert.Less(landedAt-airborneAt,.85f);
    }
    [UnityTest] public IEnumerator RunningThroughAllThreeBananasCollectsEachExactlyOnce()
    {
        PlaygroundPickup[] bananas=game.Pickups.Where(p=>p.AutoCollect).OrderBy(p=>p.transform.position.x).ToArray();Assert.AreEqual(3,bananas.Length);
        game.enabled=false;
        foreach(PlaygroundPickup banana in bananas)
        {
            PlacePlayer(banana.transform.position+Vector3.down*.8f+Vector3.back*1.3f);
            for(int i=0;i<24;i++){game.GetComponent<CharacterController>().Move(Vector3.forward*.12f+Vector3.down*.02f);yield return new WaitForFixedUpdate();}
            Assert.IsTrue(banana.Collected);
        }
        Assert.AreEqual(3,game.Collected);yield return new WaitForSeconds(.1f);Assert.AreEqual(3,game.Collected);
    }
    [UnityTest] public IEnumerator WalkingIntoAChairPushesItsRigidbody()
    {
        Rigidbody chair=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(r=>r.name=="Coral cafe chair").OrderBy(r=>r.position.z).ThenBy(r=>r.position.x).First();Vector3 before=chair.position;
        game.OrbitCamera.Orbit(-game.OrbitCamera.Yaw,0);PlacePlayer(before+Vector3.back*1.3f+Vector3.up*.05f);
        game.enabled=false;float until=Time.time+.9f;
        while(Time.time<until){game.MoveCharacter(Vector3.forward,false,false,Time.deltaTime);yield return null;}
        Assert.Greater(Vector3.Distance(before,chair.position),.2f);Assert.Less(chair.linearVelocity.magnitude,5);
    }
    [UnityTest] public IEnumerator RunningIntoATableMovesItAndSpillsItsCups()
    {
        Rigidbody table=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(r=>r.name=="Pushable dining table").OrderBy(r=>r.position.z).First();
        Vector3 before=table.position;PlacePlayer(before+Vector3.back*1.6f+Vector3.up*.05f);game.enabled=false;
        float until=Time.time+1.3f;
        while(Time.time<until){game.MoveCharacter(Vector3.forward,false,true,Time.deltaTime);yield return null;}
        Assert.Greater(Vector3.Distance(before,table.position),.2f);
        yield return new WaitForSeconds(1.5f);
        Assert.Greater(BadShiftImpactProp.BreakCount,0,"A cup should fall and break after the table is knocked");
    }
    [UnityTest] public IEnumerator FallingCupBreaksOnceAndPlaysTheAuthoredEvent()
    {
        BadShiftImpactProp cup=Object.FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None).First(p=>p.Breakable);
        Rigidbody body=cup.GetComponent<Rigidbody>();body.position=new Vector3(3,2,-5);body.linearVelocity=Vector3.zero;body.WakeUp();
        float until=Time.time+2;
        while(cup!=null && !cup.Broken && Time.time<until)yield return null;
        Assert.AreEqual(1,BadShiftImpactProp.BreakCount);
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(r=>r.EventName=="CeramicBreak"&&r.Outcome==PlaybackOutcome.Played));
        Assert.AreEqual(6,Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m=>m.name=="Ceramic shard"));
        yield return new WaitForSeconds(.3f);Assert.AreEqual(1,BadShiftImpactProp.BreakCount);
    }
    [UnityTest] public IEnumerator ManagerQuoteUsesTheActiveVoiceLine()
    {
        BadShiftDemo demo=game.GetComponent<BadShiftDemo>();Quaternion before=demo.Boss.rotation;
        yield return new WaitForSeconds(.4f);Assert.Greater(Quaternion.Angle(before,demo.Boss.rotation),.2f);
        game.Speak();yield return null;Assert.IsNotEmpty(demo.BossQuote);Assert.AreEqual(AudioSystem.VoiceOver.CurrentLine.Subtitle,demo.BossQuote);
    }
    [UnityTest] public IEnumerator ResetRestoresPickupsFurnitureAndPauseState()
    {
        PlaygroundPickup pickup=game.Pickups.First(p=>p.AutoCollect);PlacePlayer(pickup.transform.position+Vector3.down*.8f);game.TryCollect(pickup);
        Rigidbody chair=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).First(r=>r.name=="Coral cafe chair");Vector3 original=chair.position;chair.position+=Vector3.right*2;
        AudioSystem.SetPaused(true);Time.timeScale=0;AudioMWPlayground old=game;game.ResetScene();game.ResetScene();
        float until=Time.realtimeSinceStartup+5;while(old!=null&&Time.realtimeSinceStartup<until)yield return null;
        Assert.IsTrue(old==null);yield return null;yield return new WaitForSeconds(.2f);
        game=Object.FindFirstObjectByType<AudioMWPlayground>();Assert.AreEqual(0,game.Collected);Assert.IsTrue(game.Pickups.All(p=>!p.Collected));Assert.AreEqual(1,Time.timeScale);Assert.IsFalse(AudioListener.pause);
        Assert.IsTrue(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Any(r=>r.name=="Coral cafe chair"&&Vector3.Distance(r.position,original)<.15f));
        Assert.AreEqual(1,game.Bank.ScopeCount);Assert.AreEqual(0,BadShiftImpactProp.BreakCount);
        Assert.AreEqual(4,Object.FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None).Count(p=>p.Breakable));
        BadShiftDemo demo=game.GetComponent<BadShiftDemo>();var clips=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<demo.MicrowaveBag.Clips.Length;i++){demo.PlayMicrowave();clips.Add(demo.LastAnswer);yield return null;}
        Assert.AreEqual(demo.MicrowaveBag.Clips.Length,clips.Count);
    }
}
