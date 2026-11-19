using System.Collections;
using System.Linq;
using AudioMW;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public sealed class AudioMWCatalogPlayer : MonoBehaviour
{
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        const string key = "audiomw-validation/kitchen-bass";
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
