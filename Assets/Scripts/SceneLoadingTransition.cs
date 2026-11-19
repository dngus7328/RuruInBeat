using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reusable, asset-free loading overlay shared by Title, Song Select and Mouse.
/// Does not create another scene or modify any scene's authored UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneLoadingTransition : MonoBehaviour
{
    private static SceneLoadingTransition current;

    private Canvas loadingCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform barFill;
    private TMP_Text progressText;
    private Coroutine running;

    private const float MinimumVisibleSeconds = 0.8f;
    private const float FadeSeconds = 0.2f;

    public static bool IsLoading => current != null && current.running != null;

    /// <summary>Begin a scene change with a reusable loading screen.</summary>
    public static bool Load(string sceneName)
    {
        if (IsLoading)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[SceneLoading] Scene is not available in Build Settings: " +
                sceneName);
            return false;
        }

        if (current == null)
        {
            GameObject root = new GameObject("SceneLoadingTransition");
            current = root.AddComponent<SceneLoadingTransition>();
        }

        current.running = current.StartCoroutine(current.ChangeScene(sceneName));
        return true;
    }

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        GameObject layer = new GameObject("SharedLoadingCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        layer.transform.SetParent(transform, false);

        loadingCanvas = layer.GetComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = 30000;

        CanvasScaler scaler = layer.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayGroup = layer.GetComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;

        RectTransform background = AddRect("MatteBackground", layer.transform,
            Vector2.zero, Vector2.one);
        Image bg = background.gameObject.AddComponent<Image>();
        bg.color = new Color(0.085f, 0.075f, 0.118f, 1f);
        bg.raycastTarget = true;

        RectTransform heading = AddRect("LoadingTitle", layer.transform,
            new Vector2(.2f, .505f), new Vector2(.8f, .57f));
        TMP_Text title = AddText(heading, "LOADING", 36f,
            new Color(.95f, .91f, .96f, 1f));
        title.characterSpacing = 4f;

        RectTransform track = AddRect("LoadingTrack", layer.transform,
            new Vector2(.34f, .475f), new Vector2(.66f, .484f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(.24f, .20f, .30f, 1f);
        trackImage.raycastTarget = false;

        barFill = AddRect("LoadingFill", track,
            Vector2.zero, new Vector2(0f, 1f));
        Image fillImage = barFill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(.78f, .64f, .82f, 1f);
        fillImage.raycastTarget = false;

        RectTransform percentage = AddRect("LoadingPercent", layer.transform,
            new Vector2(.2f, .418f), new Vector2(.8f, .46f));
        progressText = AddText(percentage, "00%", 24f,
            new Color(.73f, .67f, .79f, 1f));

        loadingCanvas.enabled = false;
    }

    private static RectTransform AddRect(string name, Transform parent,
        Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text AddText(RectTransform rect, string value,
        float size, Color color)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.text = value;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    private void DisplayProgress(float progress)
    {
        float amount = Mathf.Clamp01(progress);
        barFill.anchorMax = new Vector2(amount, 1f);
        progressText.text = Mathf.RoundToInt(amount * 100f).ToString("00") + "%";
    }

    private IEnumerator ChangeScene(string sceneName)
    {
        loadingCanvas.enabled = true;
        overlayGroup.alpha = 0f;
        DisplayProgress(0f);

        float begin = Time.realtimeSinceStartup;
        float fade = 0f;
        while (fade < FadeSeconds)
        {
            fade += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Clamp01(fade / FadeSeconds);
            yield return null;
        }
        overlayGroup.alpha = 1f;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError("[SceneLoading] Could not begin loading: " + sceneName);
            loadingCanvas.enabled = false;
            running = null;
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < .9f ||
            Time.realtimeSinceStartup - begin < MinimumVisibleSeconds)
        {
            DisplayProgress(Mathf.Clamp01(operation.progress / .9f));
            yield return null;
        }

        DisplayProgress(1f);
        yield return new WaitForSecondsRealtime(.1f);
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        // A paused Mouse scene must never leave the next scene frozen.
        Time.timeScale = 1f;
        yield return null;

        fade = FadeSeconds;
        while (fade > 0f)
        {
            fade -= Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Clamp01(fade / FadeSeconds);
            yield return null;
        }

        loadingCanvas.enabled = false;
        running = null;
    }
}
