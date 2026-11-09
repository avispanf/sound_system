using System.Collections;
using AudioMW;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlaygroundTestCleanup
{
    public static IEnumerator Clear()
    {
        Time.timeScale = 1;
        AudioSystem.SetPaused(false);
        Scene scene = SceneManager.GetSceneByName("RelayField");
        if (scene.IsValid() && scene.isLoaded)
        {
            Scene empty = SceneManager.CreateScene("AudioMW test cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        AudioSystem.Shutdown();
        yield return null;
    }
}
