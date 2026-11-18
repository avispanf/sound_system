using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BadShiftDaylightTests
{
    [UnityTearDown]
    public IEnumerator Clear() { yield return PlaygroundTestCleanup.Clear(); }

    [UnityTest]
    public IEnumerator CycleButtonLocksNightThenDayAndRestoresAutomaticSunlight()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");
        yield return null;
        var sky=Object.FindFirstObjectByType<BadShiftDemo>().Daylight;
        Assert.IsNotNull(sky);
        Assert.AreEqual(BadShiftSkyMode.Auto,sky.Mode);
        Assert.AreEqual("Sunset",sky.Period);
        Assert.IsTrue(sky.Shafts[0].enabled);
        Assert.IsTrue(GameObject.Find("Cafeteria coral sign").GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"));
        sky.CycleMode();
        yield return new WaitForSeconds(1.3f);
        Assert.AreEqual("Light: Night",sky.ModeLabel);
        Assert.IsTrue(sky.OfficeLights[0].enabled);
        Assert.Greater(sky.OfficeLights[0].intensity,10);
        Assert.Greater(sky.OfficeLights[0].color.b,.94f);
        Assert.IsFalse(sky.Shafts[0].enabled);
        sky.CycleMode();
        yield return new WaitForSeconds(1.3f);
        Assert.AreEqual("Light: Day",sky.ModeLabel);
        Assert.IsFalse(sky.OfficeLights[0].enabled);
        Assert.AreEqual(.5f,sky.Phase,.001f);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(.5f,sky.Phase,.001f);
        sky.CycleSeconds=12;
        sky.CycleMode();
        var periods=new HashSet<string>();
        float deadline=Time.realtimeSinceStartup+18;
        while (periods.Count<7 && Time.realtimeSinceStartup<deadline)
        {
            periods.Add(sky.Period);
            yield return null;
        }
        CollectionAssert.IsSubsetOf(new[]{"Sunrise","Day","Evening","Sunset","Dusk","Night","Twilight"},periods);
    }
}
