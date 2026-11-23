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
                CaptureLiveScene(Path.Combine(dir, "Title_Main.png"));
                GameObject settings = FindInScene("SettingsPanel");
                Assert.IsNotNull(settings);
                settings.SetActive(true);
                yield return new WaitForSecondsRealtime(.20f);
            }

            // WaitForEndOfFrame is not processed by Unity Editor batchmode.
            // Render the real scene and its canvases explicitly to an
            // offscreen GameView-sized target instead.
            yield return null;
            string file = Path.Combine(dir, files[i]);
            CaptureLiveScene(file);
            Assert.IsTrue(File.Exists(file),
                "Real game frame was not exported: " + file);
            Assert.Greater(new FileInfo(file).Length, 1500L,
                "Captured frame contains no usable PNG image data");
            yield return null;
        }
#else
        Assert.Ignore("Requires real Unity Editor GameView.");
        yield break;
#endif
    }

    private static void CaptureLiveScene(string outputPath)
    {
        const int width = 1920;
        const int height = 1080;
        GameObject cameraRoot = new GameObject("CI_OffscreenCaptureCamera");
        Camera camera = cameraRoot.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.nearClipPlane = .3f;
        camera.farClipPlane = 1000f;
        camera.transform.position = new Vector3(0f, 0f, -500f);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .065f, .12f, 1f);
        camera.cullingMask = ~0;

        RenderTexture target = new RenderTexture(
            width, height, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture = target;

        // Screen Space Overlay is GameView-only. Switch the same real
        // Canvas to screen-space camera while rendering this one frame.
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        RenderMode[] modes = new RenderMode[canvases.Length];
        Camera[] worldCameras = new Camera[canvases.Length];
        float[] planeDistances = new float[canvases.Length];
        for (int k = 0; k < canvases.Length; k++)
        {
            Canvas canvas = canvases[k];
            modes[k] = canvas.renderMode;
            worldCameras[k] = canvas.worldCamera;
            planeDistances[k] = canvas.planeDistance;
            if (!canvas.gameObject.scene.IsValid() ||
                canvas.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
            }
        }

        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture prior = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D frame = new Texture2D(
            width, height, TextureFormat.RGBA32, false);
        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        frame.Apply(false, false);
        File.WriteAllBytes(outputPath, frame.EncodeToPNG());
        RenderTexture.active = prior;

        for (int k = 0; k < canvases.Length; k++)
        {
            if (canvases[k] == null) continue;
            canvases[k].renderMode = modes[k];
            canvases[k].worldCamera = worldCameras[k];
            canvases[k].planeDistance = planeDistances[k];
        }
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(frame);
        Object.Destroy(cameraRoot);
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
