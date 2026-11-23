using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Integration checks against real authored Unity scenes and imported sprites.
/// No mock scene or test-only UI is substituted for the shipped game.
/// </summary>
public sealed class RuruVisualArtPlayModeTests
{
    private static readonly string[] RequiredArt =
    {
        "Characters/Lulu_Master", "Characters/Pino_Master",
        "SongSelect/Backdrop", "SongSelect/ShelfPanel",
        "SongSelect/InfoPanel", "SongSelect/TrackSlot_Normal",
        "SongSelect/TrackSlot_Active", "SongSelect/JacketMount",
        "SongSelect/ActionButton", "Tutorial/DialoguePanel",
        "Tutorial/GuidePanel", "Tutorial/DialogueButton",
        "Story/LuluRoom_Background"
    };

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform result = Find(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static Transform SceneObject(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform result = Find(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    private static IEnumerator LoadSceneAndVisuals(string sceneName)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName,
            LoadSceneMode.Single);
        Assert.IsNotNull(load, "Missing scene: " + sceneName);
        yield return load;
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(sceneName,SceneManager.GetActiveScene().name);
        Assert.IsNotNull(SceneObject("RuruVisualSceneComposer"));
    }

    [Test]
    public void EverySceneArtworkFileLoadsAsASprite()
    {
        foreach (string relative in RequiredArt)
        {
            Sprite sprite = Resources.Load<Sprite>("RuruArt/Scenes/" + relative);
            Assert.IsNotNull(sprite, "Missing or non-Sprite image: " + relative);
            Assert.Greater(sprite.rect.width, 32f);
            Assert.Greater(sprite.rect.height, 32f);
        }
    }

    [UnityTest]
    public IEnumerator SongSelectLoadsOriginalCatalogWithIndependentArtwork()
    {
        yield return LoadSceneAndVisuals("SongSelectScene");
        Assert.IsNotNull(SceneObject("SongRow_01"));
        Assert.IsNotNull(SceneObject("SongRow_06"));
        Assert.IsNotNull(SceneObject("Ruru_ShelfArt"));
        Assert.IsNotNull(SceneObject("Ruru_InfoArt"));
        Assert.IsNotNull(SceneObject("Ruru_JacketMount"));
        Assert.IsNotNull(SceneObject("PlaySelected"));
        for (int i = 1; i <= 6; i++)
        {
            Transform row = SceneObject("SongRow_"+i.ToString("00"));
            Assert.IsNotNull(row);
            Transform image = Find(row,"Ruru_TrackArtwork");
            Assert.IsNotNull(image,"Missing card art: "+i);
            Assert.IsFalse(image.GetComponent<Image>().raycastTarget,
                "Art image must not intercept original selection click");
        }
        Transform play = SceneObject("PlaySelected");
        Assert.IsTrue(play.gameObject.activeInHierarchy);
        Assert.IsNotNull(play.GetComponent<Button>());
        Transform backdrop = SceneObject("Background");
        Assert.IsNotNull(backdrop);
        Assert.AreSame(Resources.Load<Sprite>("RuruArt/Scenes/SongSelect/Backdrop"),
            backdrop.GetComponent<Image>().sprite);
    }

    [UnityTest]
    public IEnumerator TutorialUsesApprovedPortraitsAndNonBlockingGuide()
    {
        yield return LoadSceneAndVisuals("Tutorial");
        Transform conversation = SceneObject("TutorialConversation");
        Assert.IsNotNull(conversation);
        Assert.IsNotNull(Find(conversation,"Ruru_RoomIllustration"));
        Assert.IsNotNull(Find(conversation,"Ruru_Lulu"));
        Assert.IsNotNull(Find(conversation,"Ruru_Pino"));
        Transform panel = SceneObject("DialoguePanel");
        Assert.IsNotNull(panel);
        Assert.AreSame(Resources.Load<Sprite>("RuruArt/Scenes/Tutorial/DialoguePanel"),
            panel.GetComponent<Image>().sprite);
        Transform guide = SceneObject("TutorialGuide");
        Assert.IsNotNull(guide);
        foreach (Graphic graphic in guide.GetComponentsInChildren<Graphic>(true))
            Assert.IsFalse(graphic.raycastTarget,
                "Guide intercepted gameplay mouse input: " + graphic.name);
        Assert.IsNotNull(SceneObject("NextDialogue"),
            "The original tutorial dialogue button must remain interactive");
    }
}