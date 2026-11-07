using System.Collections;
using System.Linq;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RelayFieldIntegrationTests
{
    private AudioMWPlayground game;
    private Keyboard addedKeyboard;
    [UnitySetUp] public IEnumerator Load()
    {
        if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>();
        yield return SceneManager.LoadSceneAsync("RelayField"); yield return null;
        game = Object.FindFirstObjectByType<AudioMWPlayground>(); Assert.IsNotNull(game);
    }
    [UnityTearDown] public IEnumerator Stop()
    {
        AudioSystem.Shutdown();
        if (addedKeyboard != null) InputSystem.RemoveDevice(addedKeyboard);
        addedKeyboard = null; yield return null;
    }
    [UnityTest] public IEnumerator GameplayActionsUseRealRuntimeAndLimits()
    {
        Assert.IsTrue(game.Bank.IsFullyLoaded()); game.Fire();
        EventDebugRecord record; Assert.IsTrue(AudioSystem.Debugger.TryGetLast(out record)); Assert.AreEqual(PlaybackOutcome.Played,record.Outcome);
        game.StressLimit(); AudioSystem.Debugger.TryGetLast(out record);
        Assert.AreEqual(PlaybackOutcome.RejectedInstanceLimit,record.Outcome); Assert.AreEqual(2,record.Rejection.CurrentCount);
        game.Speak(); yield return null; Assert.IsTrue(AudioSystem.VoiceOver.IsSpeaking);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.LessOrEqual(AudioSystem.ActiveVoiceCount,3, string.Join(" | ",AudioSystem.Debugger.Records.GroupBy(item => item.EventName).Select(group => group.Key + ":" + group.Count())));
    }
    [UnityTest] public IEnumerator LandingFiresOnceAfterAnActualFall()
    {
        yield return new WaitForSeconds(0.6f);
        int before = AudioSystem.Debugger.Records.Count(item => item.EventName == "Landing");
        CharacterController character = game.GetComponent<CharacterController>();
        character.enabled = false; game.transform.position += Vector3.up * 2; character.enabled = true;
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(before + 1, AudioSystem.Debugger.Records.Count(item => item.EventName == "Landing"));
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(before + 1, AudioSystem.Debugger.Records.Count(item => item.EventName == "Landing"));
    }
    [UnityTest] public IEnumerator CollectingRelaysAndReachingExitCompletesTheRoute()
    {
        CharacterController character = game.GetComponent<CharacterController>();
        foreach (PlaygroundPickup pickup in game.Pickups)
        {
            character.enabled = false; game.transform.position = pickup.transform.position + Vector3.down * 0.8f; character.enabled = true;
            Physics.SyncTransforms(); game.Interact(); yield return null;
        }
        Assert.AreEqual(5,game.Collected);
        character.enabled = false; game.transform.position = game.Exit.position + Vector3.up * 0.1f; character.enabled = true;
        yield return null; Assert.IsTrue(game.IsComplete);
    }
    [UnityTest] public IEnumerator InstalledMixRoutesAudioAndDucksWithoutSourceDuplication()
    {
        AudioMixProfile mix = AudioRuntime.Instance.ActiveMixProfile; Assert.IsNotNull(mix);
        Assert.AreEqual("AmbienceDuck", game.Ambience.MixerGroup.name); Assert.AreEqual("VO", game.Lines[0].MixerGroup.name);
        Assert.IsFalse(game.Ambience.ParameterBindings.Any(binding => binding.Parameter == game.VoiceDuck && binding.Target == ParameterTarget.Volume));
        game.Speak(); yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(1, AudioSystem.GetParameter(game.VoiceDuck), 0.01f);
        Assert.IsTrue(mix.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb", out float db)); Assert.AreEqual(-6, db, 0.05f);
        AudioSystem.StopVoiceOver(); yield return new WaitForSecondsRealtime(mix.ReleaseSeconds + 0.1f);
        Assert.AreEqual(0, AudioSystem.GetParameter(game.VoiceDuck), 0.01f);
        mix.Routing.Mixer.GetFloat("AudioMW_AmbienceDuckDb", out db); Assert.AreEqual(0, db, 0.05f);
    }
    [UnityTest] public IEnumerator MusicRequestsAndSceneReloadKeepBankOwnershipBounded()
    {
        Assert.AreEqual(1,game.Bank.ScopeCount);
        game.RequestMusic(true); double boundary = AudioSystem.Music.PendingStartDspTime; game.RequestMusic(true);
        Assert.AreEqual(boundary,AudioSystem.Music.PendingStartDspTime);
        SoundBank bank = game.Bank;
        yield return SceneManager.LoadSceneAsync("RelayField"); yield return null;
        Assert.AreEqual(1,bank.ScopeCount);
        Assert.LessOrEqual(AudioSystem.ActiveVoiceCount,2);
    }
}
