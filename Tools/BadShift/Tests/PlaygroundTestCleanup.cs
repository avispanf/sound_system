using System.Collections;
using AudioMW;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlaygroundTestCleanup
{
    public static IEnumerator ResetAndWait(AudioMWPlayground game)
    {
        int previousScene = game.gameObject.scene.handle;
        game.ResetScene();
        double deadline = Time.realtimeSinceStartupAsDouble + 10;
        while ((game != null || SceneManager.GetActiveScene().handle == previousScene) && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        Assert.IsTrue(game == null,"The old playground survived the scene reset.");
        Assert.AreNotEqual(previousScene,SceneManager.GetActiveScene().handle,"The replacement scene was not activated.");
        yield return null;
    }

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
