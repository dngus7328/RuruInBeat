using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Exports actual rendered Unity GameView images for visual inspection.
/// Uses live scenes, original fonts, Canvas and 2D sprites. Never constructs
/// a substitute mockup. Saved under CI's uploaded artifacts directory.
/// </summary>
public sealed class RuruRealScreenCapturePlayModeTests
{
    [UnityTest]
    public IEnumerator CaptureSongSelectSettingsAndTutorialStoryInRealGameView()
    {
#if UNITY_EDITOR
        string dir = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "artifacts", "RealGameScreens");
        Directory.CreateDirectory(dir);

        string[] scenes = { "SongSelectScene", "TitleScene", "Tutorial" };
        string[] files = { "SongSelect.png", "Settings.png", "TutorialStory.png" };

        for (int i = 0; i < scenes.Length; i++)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AsyncOperation op = SceneManager.LoadSceneAsync(
                scenes[i], LoadSceneMode.Single);
            Assert.IsNotNull(op);
            yield return op;
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(.20f);

            if (scenes[i] == "TitleScene")
            {
                GameObject settings = FindInScene("SettingsPanel");
                Assert.IsNotNull(settings);
                settings.SetActive(true);
                yield return new WaitForSecondsRealtime(.20f);
            }

            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.IsNotNull(frame, "Actual game screenshot is unavailable");
            byte[] data = frame.EncodeToPNG();
            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 1500,
                "Captured frame contains no usable PNG image data");
            string file = Path.Combine(dir, files[i]);
            File.WriteAllBytes(file, data);
            Assert.IsTrue(File.Exists(file),
                "Real game frame was not exported: " + file);
            Object.Destroy(frame);
            yield return null;
        }
#else
        Assert.Ignore("Requires real Unity Editor GameView.");
        yield break;
#endif
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform match = FindRecursive(child, name);
            if (match != null) return match;
        }
        return null;
    }
}
