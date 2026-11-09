using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public sealed class BadShiftTourTests
{
    [UnityTearDown] public IEnumerator Cleanup() { yield return PlaygroundTestCleanup.Clear(); }
    [UnityTest,Timeout(150000)] public IEnumerator GuidedTourCompletesUsingRealMovementAndEvents()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.5f);
        BadShiftTour tour=Object.FindFirstObjectByType<BadShiftTour>();tour.Begin();
        float deadline=Time.realtimeSinceStartup+120;
        while(tour.Running&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(tour.Running);Assert.AreEqual("Demo complete / Your turn",tour.Heading,tour.Explanation);
        Assert.GreaterOrEqual(tour.Game.Collected,3);
        Assert.Greater(tour.Game.GetComponent<BadShiftDemo>().ListenerDistance,8);
        Assert.IsTrue(AudioMW.AudioSystem.Debugger.Records.Count>10);
    }
    [UnityTest] public IEnumerator ReplayReloadsSceneAndStartsTour()
    {
        yield return SceneManager.LoadSceneAsync("RelayField"); yield return new WaitForSeconds(.5f);
        BadShiftTour original = Object.FindFirstObjectByType<BadShiftTour>();
        original.WatchFromStart();
        float deadline = Time.realtimeSinceStartup + 5;
        BadShiftTour replacement = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            replacement = Object.FindFirstObjectByType<BadShiftTour>();
            if (replacement != null && replacement != original && replacement.Running) break;
            yield return null;
        }
        Assert.IsNotNull(replacement); Assert.AreNotSame(original, replacement); Assert.IsTrue(replacement.Running);
        Assert.IsFalse(BadShiftTour.StartOnLoad); Assert.AreEqual(0, replacement.Game.Collected);
        replacement.Stop();
    }
    [UnityTest] public IEnumerator ManualControlStopsTourAndClearsMovement()
    {
        yield return SceneManager.LoadSceneAsync("RelayField");yield return new WaitForSeconds(.5f);
        BadShiftTour tour=Object.FindFirstObjectByType<BadShiftTour>();tour.Begin();yield return new WaitForSeconds(.2f);tour.Stop();
        Assert.IsFalse(tour.Running);Assert.AreEqual(Vector3.zero,tour.Direction);
    }
}
