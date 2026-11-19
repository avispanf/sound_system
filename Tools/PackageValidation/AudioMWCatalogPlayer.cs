using System;
using System.IO;
using System.Collections;
using System.Linq;
using AudioMW;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public sealed class AudioMWCatalogPlayer : MonoBehaviour
{
    public const string RemoteKey = "audiomw-validation/remote-kitchen-bass";
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        string url = Environment.GetEnvironmentVariable("AUDIOMW_REMOTE_CATALOG");
        string key = "audiomw-validation/kitchen-bass";
        if (!string.IsNullOrEmpty(url))
        {
            var original = Addressables.LoadResourceLocationsAsync(RemoteKey,typeof(AudioClip));
            yield return original;
            bool absent = original.Status == AsyncOperationStatus.Succeeded && original.Result.Count == 0;
            Addressables.Release(original);
            if (!absent) { Debug.LogError("Remote clip must be absent from the installed catalog."); Application.Quit(6); yield break; }
            var catalog = Addressables.LoadContentCatalogAsync(url,false);
            yield return catalog;
            bool loaded = catalog.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(catalog);
            if (!loaded) { Debug.LogError("HTTP catalog unavailable."); Application.Quit(7); yield break; }
            key = RemoteKey;
        }
        var locations = Addressables.LoadResourceLocationsAsync(key, typeof(AudioClip));
        yield return locations;
        if (locations.Status != AsyncOperationStatus.Succeeded || locations.Result.Count != 1 || locations.Result[0].ProviderId != typeof(BundledAssetProvider).FullName)
        {
            Debug.LogError("Native catalog location did not resolve to the bundled provider.");
            Application.Quit(2);
            yield break;
        }
        string provider = locations.Result[0].ProviderId;
        Addressables.Release(locations);
        var loader = new AddressableAudioAssetLoader();
        bool done = false;
        AudioClip clip = null;
        if (key == RemoteKey)
        {
            loader.LoadAsync(key,(address,audio) => { clip = audio; done = true; });
            double failureDeadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!done && Time.realtimeSinceStartupAsDouble < failureDeadline) yield return null;
            if (!done || clip != null || loader.RetainedAddressCount != 1)
            {
                Debug.LogError("Interrupted transfer must complete with an unavailable clip.");
                Application.Quit(8);
                yield break;
            }
            loader.Release(key);
            if (loader.RetainedAddressCount != 0) { Debug.LogError("Failed request still retained."); Application.Quit(9); yield break; }
            File.WriteAllText(Environment.GetEnvironmentVariable("AUDIOMW_RETRY_MARKER"),"released");
            Debug.Log("REMOTE_CATALOG interrupted=True released=True");
            done = false;
        }
        loader.LoadAsync(key, (address, audio) => { clip = audio; done = true; });
        double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (!done && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        if (clip == null || clip.samples != 384000 || clip.channels != 2 || clip.frequency != 48000)
        {
            Debug.LogError("Native loader returned an unexpected clip.");
            Application.Quit(3);
            yield break;
        }
        float[] pcm = new float[8192];
        if (!clip.GetData(pcm, 24000) || !pcm.Any(value => Mathf.Abs(value) > .0001f))
        {
            Debug.LogError("Non-silent PCM unavailable.");
            Application.Quit(4);
            yield break;
        }
        var sound = SoundEvent.CreateRuntime(clip);
        var handle = AudioSystem.PlayTracked(sound);
        yield return new WaitForSecondsRealtime(.3f);
        var voice = handle.Voice;
        int sample = voice != null && voice.Source.isPlaying ? voice.Source.timeSamples : 0;
        handle.Stop();
        AudioSystem.Shutdown();
        loader.ReleaseAll();
        bool ok = sample > 0 && AudioListener.volume == 0 && loader.RetainedAddressCount == 0;
        Debug.Log("PACKED_PLAYER_RUN " + ok + " unity=" + Application.unityVersion + " provider=" + provider + " sample=" + sample + " pcm=True retained=" + loader.RetainedAddressCount + " muted=" + (AudioListener.volume == 0));
        Application.Quit(ok ? 0 : 5);
    }
}
