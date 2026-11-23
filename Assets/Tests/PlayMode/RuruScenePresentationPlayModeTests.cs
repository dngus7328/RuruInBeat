using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Integration checks for actual Unity screen hierarchy, presentation and
/// interaction bindings. Tests the real scenes, not screenshots or mock UIs.
/// </summary>
public sealed class RuruScenePresentationPlayModeTests
{
    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform SceneObject(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = Find(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static IEnumerator LoadAndBind(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        Assert.IsNotNull(op);
        yield return op;
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
        Assert.IsNotNull(SceneObject("RuruScenePresentation"),
            "Presentation controller did not attach in " + scene);
    }

    [UnityTest]
    public IEnumerator SongSelect_PreservesRealCartridgesAndSelectedTrack()
    {
        yield return LoadAndBind("SongSelectScene");
        Assert.IsNotNull(SceneObject("SongRow_01"));
        Assert.IsNotNull(SceneObject("SongRow_06"));
        Transform selected = SceneObject("SelectedTrackPanel");
        Assert.IsNotNull(selected);
        Assert.IsNotNull(selected.GetComponent<CanvasGroup>(),
            "Selected panel needs animation group");
        Assert.IsNotNull(SceneObject("JacketImage_Replaceable"),
            "Original cover artwork area must remain intact");
        for (int i = 0; i < 15; i++) yield return null;
        Assert.Greater(selected.GetComponent<CanvasGroup>().alpha, .99f);
        Assert.IsNotNull(SceneObject("PlayButton"),
            "Do not remove the original Play control");
    }

    [UnityTest]
    public IEnumerator TitleSettings_MouseControlsExistWithoutLegacyLaneRows()
    {
        yield return LoadAndBind("TitleScene");
        Transform panel = SceneObject("SettingsPanel");
        Assert.IsNotNull(panel);
        panel.gameObject.SetActive(true);
        yield return null;
        yield return null;

        Transform page = SceneObject("ControlPage");
        Assert.IsNotNull(page);
        Transform mouseControls = Find(page, "MouseControlsVisual");
        Assert.IsNotNull(mouseControls,
            "Mouse-only control help is missing");
        Assert.AreEqual(4, mouseControls.childCount);
        foreach (string obsolete in new[]
        {
            "Lane1KeyRow", "Lane2KeyRow", "Lane3KeyRow",
            "Lane4KeyRow", "PinoLeftKeyRow", "PinoRightKeyRow"
        })
        {
            Transform legacy = Find(page, obsolete);
            if (legacy != null)
                Assert.IsFalse(legacy.gameObject.activeSelf,
                    "Keyboard-era row visible: " + obsolete);
        }
        Assert.IsNotNull(SceneObject("SoundPage"));
        Assert.IsNotNull(SceneObject("DisplayPage"));
    }

    [UnityTest]
    public IEnumerator Tutorial_SeparatesNarrativeFromNonBlockingPractice()
    {
        yield return LoadAndBind("Tutorial");
        Transform conversation = SceneObject("TutorialConversation");
        Transform guide = SceneObject("TutorialGuide");
        Assert.IsNotNull(conversation,
            "Runtime prologue dialogue must be created");
        Assert.IsNotNull(guide, "Runtime gameplay guide missing");
        RectTransform rect = guide as RectTransform;
        Assert.IsNotNull(rect);
        Assert.GreaterOrEqual(rect.anchorMin.y, .70f,
            "Tutorial hint overlaps central mouse playfield");
        CanvasGroup canvasGroup = guide.GetComponent<CanvasGroup>();
        Assert.IsNotNull(canvasGroup);
        Assert.IsFalse(canvasGroup.blocksRaycasts);
        Assert.IsFalse(canvasGroup.interactable);
        foreach (Graphic graphic in
                 guide.GetComponentsInChildren<Graphic>(true))
            Assert.IsFalse(graphic.raycastTarget,
                "Practice hint intercepts mouse note input: " + graphic.name);
        Assert.IsNotNull(SceneObject("DialoguePanel"));
        Assert.IsNotNull(SceneObject("NextDialogue"));
    }
}
