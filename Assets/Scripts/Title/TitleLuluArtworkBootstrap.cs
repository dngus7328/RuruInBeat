using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the separate Lulu title illustration without modifying the existing
/// title scene, logo, buttons, settings, or click handlers.
///
/// Art file:
/// Assets/Resources/TitleArt/Lulu_Title.png
///
/// The image is drawn immediately after the current Background on the title
/// Canvas, so existing Logo, MenuRoot, and dialogs stay in front.
/// </summary>
public static class TitleLuluArtworkBootstrap
{
    private const string TitleSceneName = "TitleScene";
    private const string ResourcePath = "TitleArt/Lulu_Title";
    private const string ArtworkObjectName = "LuluTitleArtwork_Runtime";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HandleDirectSceneOpen()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != TitleSceneName)
            return;

        Canvas titleCanvas = null;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene != scene)
                continue;

            // Do not accidentally attach the art to a nested settings canvas.
            if (canvas.transform.Find("Logo") != null &&
                canvas.transform.Find("MenuRoot") != null)
            {
                titleCanvas = canvas;
                break;
            }
        }

        if (titleCanvas == null)
        {
            Debug.LogWarning("[TitleArt] Main title Canvas was not found.");
            return;
        }

        if (titleCanvas.transform.Find(ArtworkObjectName) != null)
            return;

        Sprite illustration = Resources.Load<Sprite>(ResourcePath);

        // Works even if a newly copied PNG was imported as Default/Texture
        // rather than Sprite; no import-setting clicks are required.
        if (illustration == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture != null)
            {
                illustration = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }

        if (illustration == null)
        {
            // Until the separate art PNG is installed, the original title
            // remains fully usable and unchanged.
            Debug.Log("[TitleArt] Lulu_Title.png not installed; title unchanged.");
            return;
        }

        GameObject artObject = new GameObject(
            ArtworkObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        artObject.layer = LayerMask.NameToLayer("UI");
        artObject.transform.SetParent(titleCanvas.transform, false);

        RectTransform rect = artObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.275f, 0.49f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        // The 1920x1080 canvas reference layout keeps the character left of
        // the existing upper-right logo and Start / Settings / Exit buttons.
        const float targetHeight = 1020f;
        rect.sizeDelta = new Vector2(
            targetHeight * illustration.rect.width / illustration.rect.height,
            targetHeight);

        Image image = artObject.GetComponent<Image>();
        image.sprite = illustration;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        // TitleScene's direct Canvas children start with Background, then
        // Logo and MenuRoot. Insert right above the existing background.
        Transform background = titleCanvas.transform.Find("Background");
        int desiredIndex = background != null
            ? background.GetSiblingIndex() + 1
            : 0;
        artObject.transform.SetSiblingIndex(desiredIndex);
    }
}
