using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reusable console loading overlay shared by Title, Song Select and Mouse.
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
            // Screen Space Overlay must be a root Canvas, not a child
            // under an ordinary Transform.
            GameObject root = new GameObject("SceneLoadingTransition",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
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
        loadingCanvas = GetComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = 30000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayGroup = GetComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;

        RectTransform background = AddRect("MatteBackground", transform,
            Vector2.zero, Vector2.one);
        Image bg = background.gameObject.AddComponent<Image>();
        bg.color = new Color(0.085f, 0.075f, 0.118f, 1f);
        bg.raycastTarget = true;

        var stage=RuruConsoleUI.Stage(transform,"LoadingConsoleStage");
        var panel=RuruConsoleUI.Frame(stage,"LoadingConsole",.255f,.33f,.745f,.67f,RuruConsoleUI.Shell).rectTransform;
        RuruConsoleUI.Frame(panel,"LoadingDisplayHousing",.045f,.445f,.955f,.955f,RuruConsoleUI.Inset);
        RuruConsoleUI.Text(panel,"DeviceId","RURU IN BEAT / SYSTEM",.07f,.78f,.93f,.94f,23,RuruConsoleUI.Muted,display:true);
        RuruConsoleUI.Text(panel,"LoadingTitle","LOADING",.07f,.46f,.72f,.77f,51,RuruConsoleUI.Paper,display:true);
        progressText=RuruConsoleUI.Text(panel,"LoadingPercent","00%",.72f,.46f,.93f,.77f,43,RuruConsoleUI.Mint,TextAlignmentOptions.MidlineRight,true);
        var track=RuruConsoleUI.Frame(panel,"LoadingTrack",.07f,.27f,.93f,.41f,RuruConsoleUI.Inset).rectTransform;
        var inner=RuruConsoleUI.Region(track,"TrackInner",.025f,.20f,.975f,.80f);
        barFill=RuruConsoleUI.Strip(inner,"LoadingFill",0,0,0,1,RuruConsoleUI.Lilac).rectTransform;
        RuruConsoleUI.Text(panel,"LoadingHint","CONNECTING TO BEAT DEVICE",.07f,.07f,.93f,.20f,20,RuruConsoleUI.Muted,display:true);
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
        AudioListener.pause = false;
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
