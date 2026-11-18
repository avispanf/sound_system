using System.Collections;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftNightTrafficTests
{
    [UnityTearDown]
    public IEnumerator Clear() { yield return PlaygroundTestCleanup.Clear(); }

    [UnityTest]
    public IEnumerator EngineFollowsPassAndNeonStaysAtWindow()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");
        Object.FindFirstObjectByType<BadShiftDaylight>().SetMode(BadShiftSkyMode.Night);
        yield return new WaitForSeconds(.9f);
        var street=Object.FindFirstObjectByType<BadShiftNightTraffic>();
        Assert.NotNull(street);
        var first=street.Engine;
        var hum=street.Hum;
        Assert.IsTrue(first.IsPlaying);
        Assert.IsTrue(hum.IsPlaying);
        var motor=first.Voice.Source;
        Assert.AreSame(street.Car,first.Voice.AttachTarget);
        Assert.AreEqual(1,motor.spatialBlend);
        Assert.AreEqual(2.1f,motor.dopplerLevel,.001f);
        Assert.IsTrue(motor.loop);
        Assert.AreEqual(0,hum.Voice.Source.dopplerLevel);
        Assert.AreEqual(1,hum.Voice.Source.spatialBlend);
        Assert.Greater(street.NeonEvent.AttenuationPreset.EvaluateAttenuation(2),street.NeonEvent.AttenuationPreset.EvaluateAttenuation(7));
        var paint=street.Paint[0].sharedMaterial;
        float speed=street.Speed;
        var start=street.Car.position;
        yield return new WaitForSeconds(.18f);
        Assert.Greater(Vector3.Distance(start,street.Car.position),.5f);
        Assert.Less(Vector3.Distance(motor.transform.position,street.Car.position),.4f);
        Assert.Less(Vector3.Distance(hum.Voice.Source.transform.position,street.Neon.position),.01f);
        float until=Time.realtimeSinceStartup+12;
        while ((!street.Engine.IsPlaying || street.Engine.Id==first.Id) && Time.realtimeSinceStartup<until) yield return null;
        Assert.IsTrue(street.Engine.IsPlaying);
        Assert.IsFalse(first.IsPlaying);
        Assert.AreNotEqual(first.Id,street.Engine.Id);
        Assert.AreNotSame(paint,street.Paint[0].sharedMaterial);
        Assert.AreNotEqual(speed,street.Speed);
        Assert.AreEqual(hum.Id,street.Hum.Id);
        Assert.AreEqual(2,street.Car.GetComponentsInChildren<Light>().Length);
        Assert.IsTrue(Camera.main.allowHDR);
        Assert.AreEqual(3000,GameObject.Find("Clear cafe pane").GetComponent<Renderer>().sharedMaterial.renderQueue);
        street.enabled=false;
        Assert.IsFalse(street.Engine.IsPlaying);
        Assert.IsFalse(hum.IsPlaying);
    }
}
