using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftSoundscapeTests
{
    private BadShiftSoundscape sc;
    private long baseline;
    [UnitySetUp]
    public IEnumerator Load()
    {
        baseline=AudioSystem.Debugger.TryGetLast(out var record)?record.Sequence:0;
        yield return SceneManager.LoadSceneAsync("RelayField");
        yield return new WaitForSeconds(.18f);
        sc=Object.FindFirstObjectByType<BadShiftSoundscape>();
    }

    [UnityTearDown] public IEnumerator Clear(){yield return PlaygroundTestCleanup.Clear();}

    private void Move(float x,float z)
    {
        var controller=sc.Game.GetComponent<CharacterController>();controller.enabled=false;
        sc.Game.transform.position=sc.Room.TransformPoint(new Vector3(x,.10f,z));controller.enabled=true;
        Physics.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator EnterRoomUseBothFixturesAndPrimeServicePump()
    {
        var filter=sc.ListenerReverb;
        Assert.AreSame(sc.Game.Listener.gameObject,filter.gameObject);
        Assert.IsNull(filter.GetComponent<AudioSource>());
        Assert.IsNotNull(filter.GetComponent<AudioListener>());
        Move(-1,.54f);
        yield return new WaitForSeconds(.45f);
        Assert.IsTrue(sc.Inside); Assert.IsTrue(filter.enabled); Assert.AreEqual(1,sc.RoomMix,.02f);
        Assert.AreEqual(0,filter.dryLevel);Assert.Greater(filter.room,-600);
        Assert.AreEqual(BadShiftUseKind.Sink,sc.Game.Life.Nearest().Kind);
        sc.Game.Interact();yield return null;
        Assert.IsTrue(sc.Water.IsPlaying);Assert.IsTrue(sc.Stream.activeSelf);Assert.IsTrue(sc.PumpMotor.IsPlaying);
        sc.Game.Interact();yield return null;
        Assert.IsFalse(sc.Water.IsPlaying);Assert.IsFalse(sc.Stream.activeSelf);Assert.IsFalse(sc.PumpMotor.IsPlaying);
        Move(.75f,.63f);yield return null;
        Assert.AreEqual(BadShiftUseKind.Toilet,sc.Game.Life.Nearest().Kind);
        sc.Game.Interact();sc.Game.Interact();yield return null;
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(x=>x.Sequence>baseline && x.EventName==sc.Flush.name));
        Vector3 service=sc.Room.InverseTransformPoint(sc.Pump.position+Vector3.back*.85f);
        Move(service.x,service.z);yield return new WaitForSeconds(.5f);
        Assert.IsFalse(sc.Inside);Assert.IsFalse(filter.enabled);
        Assert.AreEqual(BadShiftUseKind.Pump,sc.Game.Life.Nearest().Kind);
        sc.Game.Interact();yield return null;Assert.IsTrue(sc.PumpMotor.IsPlaying);
    }

    [UnityTest]
    public IEnumerator CourtyardMillControlsOneVoiceAndClearsTheDoorway()
    {
        var mill=sc.Mill;
        Assert.IsNotNull(mill);
        Assert.IsNull(GameObject.Find("Slim cafe ceiling fixture"));
        Assert.IsNull(GameObject.Find("Neutral white diffuser"));
        Assert.AreEqual(2,sc.Sky.OfficeLights.Length);
        Assert.Greater(sc.Room.position.z-mill.transform.position.z,5);
        Assert.IsTrue(sc.Stream.transform.IsChildOf(sc.Tap));
        var approach=sc.Room.InverseTransformPoint(mill.transform.position+Vector3.back*.95f);
        Move(approach.x,approach.z);yield return null;
        Assert.AreEqual(BadShiftUseKind.CoffeeMill,sc.Game.Life.Nearest().Kind);
        sc.Game.Interact();yield return new WaitForSeconds(.1f);
        var voice=mill.Grinding;Assert.IsTrue(voice.IsPlaying);
        float pitch=voice.Voice.Source.pitch, volume=voice.Voice.Source.volume;
        var crank=mill.Crank.localRotation;
        sc.Game.Interact();yield return new WaitForSeconds(.1f);
        Assert.AreEqual(voice.Id,mill.Grinding.Id);
        Assert.Greater(voice.Voice.Source.pitch,pitch);
        Assert.Greater(voice.Voice.Source.volume,volume);
        Assert.Greater(Quaternion.Angle(crank,mill.Crank.localRotation),1);
        yield return new WaitForSeconds(4.8f);
        Assert.IsFalse(voice.IsPlaying);Assert.AreEqual(0,mill.Momentum);
    }

    [UnityTest]
    public IEnumerator ClosingShiftControlsSignTrafficLampsAmbienceAndScore()
    {
        sc.Sky.SetMode(BadShiftSkyMode.Day);yield return new WaitForSeconds(1.5f);
        Assert.IsFalse(sc.AfterHours);Assert.IsFalse(sc.Street.Hum.IsPlaying);
        Assert.IsTrue(sc.Street.Beams.All(x=>!x.enabled));
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(x=>x.Sequence>baseline && x.EventName=="Opening_Birds"));
        sc.Sky.SetMode(BadShiftSkyMode.Night);yield return new WaitForSeconds(1.5f);
        Assert.IsTrue(sc.AfterHours);Assert.IsTrue(sc.Street.Hum.IsPlaying);
        Assert.IsTrue(sc.Street.Beams.All(x=>x.enabled));
        Assert.IsTrue(AudioSystem.Debugger.Records.Any(x=>x.Sequence>baseline && x.EventName=="Closing_Crickets"));
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(x=>x.Sequence>baseline && x.EventName==sc.SwitchOn.name));
        yield return new WaitForSeconds(3);
        Assert.AreEqual("Night",sc.Game.Music.StateFor(AudioSystem.Music.CurrentTrack));
        sc.Sky.SetMode(BadShiftSkyMode.Day);yield return new WaitForSeconds(1.6f);
        Assert.IsFalse(sc.Street.Hum.IsPlaying);Assert.IsTrue(sc.SignLights.All(x=>!x.enabled));
        Assert.AreEqual(1,AudioSystem.Debugger.Records.Count(x=>x.Sequence>baseline && x.EventName==sc.SwitchOff.name));
    }

    [UnityTest]
    public IEnumerator PatrolHasDedicatedMovingSirenAndReleasesIt()
    {
        var street=sc.Street;Time.timeScale=3;
        float deadline=Time.realtimeSinceStartup+23;
        while(!street.Siren.IsPlaying && Time.realtimeSinceStartup<deadline)yield return null;
        var siren=street.Siren;
        Assert.IsTrue(siren.IsPlaying);Assert.IsTrue(street.EmergencyBar.activeInHierarchy);
        Assert.AreSame(street.Car,siren.Voice.AttachTarget);
        Assert.AreSame(street.SirenEvent,siren.Voice.CurrentEvent);
        Assert.Greater(siren.Voice.Source.dopplerLevel,0);
        yield return new WaitForSeconds(4);
        Assert.IsFalse(siren.IsPlaying);
    }
}
