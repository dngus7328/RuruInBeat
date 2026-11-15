using System.Collections;
using LuluInBeat.MouseMode.ChartSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach only to GameplayCanvas/PlayArea.
/// Keeps the existing GameDeviceFrame visible, powers on the display,
/// fades READY in, and enables ESC pause only when GO! begins.
/// Includes an optional power-off -> result-panel entry point.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MouseBootSequenceUI : MonoBehaviour
{
    [Header("Existing game components")]
    [SerializeField] private MouseModeChartPlayer chartPlayer;
    [SerializeField] private PinoMouseCursorUI pinoController;

    [Header("Power-on timing (seconds)")]
    [SerializeField, Min(0f)] private float offHoldSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float centerLineSeconds = 0.17f;
    [SerializeField, Min(0.05f)] private float screenExpandSeconds = 0.58f;
    [SerializeField, Min(0f)] private float settleSeconds = 0.35f;

    [Header("Screen light")]
    [SerializeField, Min(1f)] private float centerLineThickness = 3f;
    [SerializeField, Min(1f)] private float openingEdgeThickness = 3f;
    [SerializeField]
    private Color powerLineColor =
        new Color(0.86f, 0.80f, 0.94f, 1f);

    [Header("READY appearance (not countdown timing)")]
    [SerializeField, Min(0.05f)] private float readyFadeSeconds = 0.42f;
    [SerializeField, Range(0.8f, 1f)] private float readyStartScale = 0.95f;

    [Header("Optional result transition - connect later")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField, Min(0.1f)] private float powerOffSeconds = 0.55f;
    [SerializeField, Min(0f)] private float darkHoldBeforeResults = 0.30f;

    [Header("Diagnostics")]
    [SerializeField] private bool logProgress = false;

    private RectTransform playArea;
    private RectTransform noteLayer;
    private RectTransform overlayRoot;
    private RectTransform blackTop;
    private RectTransform blackBottom;
    private RectTransform centerLine;
    private RectTransform topEdge;
    private RectTransform bottomEdge;

    private Image centerLineImage;
    private Image topEdgeImage;
    private Image bottomEdgeImage;

    private Text countdownText;
    private RectTransform countdownRect;
    private CanvasGroup countdownGroup;
    private Vector3 countdownOriginalScale;
    private float readyElapsed;
    private bool countdownFound;
    private bool chartEnabled;
    private bool gameplayStarted;
    // Separate from gameplayStarted: Pino may move at GO, but pause only after GO disappears.
    private bool initialGoFinished;
    private bool shutdownStarted;
    private bool bootCompleted;

    public bool IsGameplayStarted { get { return gameplayStarted; } }
    public bool IsInitialGoFinished { get { return initialGoFinished; } }

    private void Awake()
    {
        gameplayStarted = false;
        initialGoFinished = false;
        playArea = GetComponent<RectTransform>();
        noteLayer = transform.Find("NoteLayer") as RectTransform;

        if (chartPlayer == null)
            chartPlayer = FindObjectOfType<MouseModeChartPlayer>();

        if (pinoController == null)
            pinoController = FindObjectOfType<PinoMouseCursorUI>();

        if (chartPlayer == null || noteLayer == null)
        {
            Debug.LogError(
                "[MouseBoot] Link Chart Player and check PlayArea/NoteLayer.",
                this);
            enabled = false;
            return;
        }

        // ChartPlayer.Start() must wait for the end of the power-on effect.
        chartPlayer.enabled = false;

        // Keeps ESC and focus-based gameplay pause unavailable before GO!.
        // Existing PinoMouseCursorUI is not modified.
        if (pinoController != null)
            pinoController.ExitGameplay();

        CreateOverlay();
    }

    private void Start()
    {
        if (enabled && overlayRoot != null)
            StartCoroutine(PowerOnRoutine());
    }

    private void LateUpdate()
    {
        if (overlayRoot != null &&
            overlayRoot.parent == transform.parent)
        {
            CopyPlayAreaRect();
        }

        if (!chartEnabled || shutdownStarted)
            return;

        FindCountdownIfNeeded();
        UpdateReadyVisual();
        UnlockGameplayAtGo();
        UnlockPauseAfterInitialGoFinishes();
    }

    private IEnumerator PowerOnRoutine()
    {
        SetOpening(0f);
        SetLineAlpha(centerLineImage, 0f);
        SetLineAlpha(topEdgeImage, 0f);
        SetLineAlpha(bottomEdgeImage, 0f);

        yield return WaitUnscaled(offHoldSeconds);

        // A short, horizontal power line appears in the screen center.
        float elapsed = 0f;
        while (elapsed < centerLineSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / centerLineSeconds);
            centerLine.localScale = new Vector3(EaseOutCubic(t), 1f, 1f);
            SetLineAlpha(centerLineImage, Mathf.Min(1f, t * 4f));
            yield return null;
        }

        centerLine.localScale = Vector3.one;
        SetLineAlpha(centerLineImage, 1f);

        // The black halves retract upward/downward to reveal the game.
        elapsed = 0f;
        while (elapsed < screenExpandSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / screenExpandSeconds);
            SetOpening(EaseInOutCubic(t));
            SetLineAlpha(centerLineImage,
                1f - Mathf.Clamp01(t / 0.22f));

            float edgeAlpha =
                (1f - SmoothStep(0.48f, 1f, t)) * 0.75f;
            SetLineAlpha(topEdgeImage, edgeAlpha);
            SetLineAlpha(bottomEdgeImage, edgeAlpha);
            yield return null;
        }

        SetOpening(1f);
        SetLineAlpha(centerLineImage, 0f);
        SetLineAlpha(topEdgeImage, 0f);
        SetLineAlpha(bottomEdgeImage, 0f);

        yield return WaitUnscaled(settleSeconds);

        RemoveOverlay();
        bootCompleted = true;

        // On the next frame ChartPlayer.Start() creates StartCountdown.
        // Our LateUpdate sees it that frame and applies READY fade-in.
        chartPlayer.enabled = true;
        chartEnabled = true;

        if (logProgress)
            Debug.Log("[MouseBoot] Power on -> countdown", this);

        // Intentionally no pinoController.EnterGameplay() here.
        // ESC pause only becomes available at GO!.
    }

    private void FindCountdownIfNeeded()
    {
        if (countdownFound || noteLayer == null)
            return;

        Transform found = noteLayer.Find("StartCountdown");
        if (found == null)
            return;

        countdownText = found.GetComponent<Text>();
        countdownRect = found as RectTransform;

        if (countdownText == null || countdownRect == null)
            return;

        countdownGroup = found.GetComponent<CanvasGroup>();
        if (countdownGroup == null)
            countdownGroup = found.gameObject.AddComponent<CanvasGroup>();

        countdownOriginalScale = countdownRect.localScale;
        readyElapsed = 0f;
        countdownFound = true;

        // This changes the appearance of the existing Text and any
        // TMP child created by MouseCountdownPresentationUI.
        countdownGroup.alpha = 0f;
        countdownRect.localScale =
            countdownOriginalScale * readyStartScale;

        if (logProgress)
            Debug.Log("[MouseBoot] READY entrance detected", this);
    }

    private void UpdateReadyVisual()
    {
        if (!countdownFound || countdownText == null ||
            countdownGroup == null || countdownRect == null)
            return;

        if (countdownText.text.Trim() == "READY" &&
            countdownText.gameObject.activeInHierarchy)
        {
            if (Application.isFocused && Time.timeScale > 0f &&
                !AudioListener.pause)
            {
                readyElapsed += Time.unscaledDeltaTime;
            }

            float t = Mathf.Clamp01(readyElapsed / readyFadeSeconds);
            float smooth = t * t * (3f - 2f * t);

            countdownGroup.alpha = smooth;
            countdownRect.localScale = countdownOriginalScale *
                Mathf.Lerp(readyStartScale, 1f, smooth);
        }
        else
        {
            // Subsequent 3,2,1,GO retain their original presentation.
            countdownGroup.alpha = 1f;
            countdownRect.localScale = countdownOriginalScale;
        }
    }

    private void UnlockGameplayAtGo()
    {
        if (gameplayStarted || pinoController == null)
            return;

        if (!Application.isFocused || Time.timeScale <= 0f ||
            AudioListener.pause)
            return;

        // Never unlock on AudioSource.isPlaying: scheduled audio and READY
        // can overlap, and that shortcut enables ESC before GO is shown.
        if (!countdownFound || countdownText == null ||
            !countdownText.gameObject.activeInHierarchy)
            return;

        // ChartPlayer shows GO! at the intended countdown boundary.
        string cue = countdownText.text.Trim();
        if (cue != "GO!" && cue != "GO")
            return;

        gameplayStarted = true;
        pinoController.EnterGameplay();

        if (logProgress)
            Debug.Log("[MouseBoot] GO! -> Pino control enabled; ESC pause remains locked", this);
    }

    // The ChartPlayer keeps StartCountdown active for 0.5s AFTER it shows GO!.
    // Pause unlocks only when that object has naturally disappeared.
    // This is deliberately separate from Pino movement (unlocked at GO).
    private void UnlockPauseAfterInitialGoFinishes()
    {
        if (initialGoFinished || !gameplayStarted ||
            shutdownStarted || countdownText == null)
            return;

        if (countdownText.gameObject.activeSelf)
            return;

        initialGoFinished = true;
        if (logProgress)
            Debug.Log("[MouseBoot] Initial GO finished -> ESC pause enabled", this);
    }

    /// <summary>
    /// Call from a future result/game-end controller after score is fixed.
    /// The power-off effect runs, then activates the assigned Result Panel.
    /// No result is triggered automatically while no result system exists.
    /// </summary>
    public void ShowResultsAfterPowerOff()
    {
        if (!bootCompleted || shutdownStarted)
            return;

        if (resultPanel == null)
        {
            Debug.LogWarning(
                "[MouseBoot] Assign Result Panel before calling shutdown.",
                this);
            return;
        }

        StartCoroutine(PowerOffRoutine());
    }

    private IEnumerator PowerOffRoutine()
    {
        shutdownStarted = true;
        gameplayStarted = false;
        initialGoFinished = false;

        if (pinoController != null)
            pinoController.ExitGameplay();

        CreateOverlay();
        SetOpening(1f);
        SetLineAlpha(centerLineImage, 0f);
        SetLineAlpha(topEdgeImage, 0.7f);
        SetLineAlpha(bottomEdgeImage, 0.7f);

        // The screen closes toward the center, reversing the startup.
        float elapsed = 0f;
        while (elapsed < powerOffSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / powerOffSeconds);
            SetOpening(1f - EaseInOutCubic(t));
            float alpha = (1f - t) * 0.7f;
            SetLineAlpha(topEdgeImage, alpha);
            SetLineAlpha(bottomEdgeImage, alpha);
            yield return null;
        }

        SetOpening(0f);
        SetLineAlpha(topEdgeImage, 0f);
        SetLineAlpha(bottomEdgeImage, 0f);
        yield return WaitUnscaled(darkHoldBeforeResults);

        // Results render above the dark display and GameDeviceFrame.
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();

        if (logProgress)
            Debug.Log("[MouseBoot] Power off -> result panel", this);
    }

    private void CreateOverlay()
    {
        if (overlayRoot != null)
            return;

        GameObject root = new GameObject(
            "ScreenPowerOverlay_Runtime", typeof(RectTransform));
        overlayRoot = root.GetComponent<RectTransform>();

        Transform parent = transform.parent;
        Transform frame = parent != null
            ? parent.Find("GameDeviceFrame") : null;

        if (parent != null && frame != null)
        {
            overlayRoot.SetParent(parent, false);
            CopyPlayAreaRect();
            overlayRoot.SetSiblingIndex(frame.GetSiblingIndex());
        }
        else
        {
            overlayRoot.SetParent(transform, false);
            Stretch(overlayRoot);
            overlayRoot.SetAsLastSibling();
            Debug.LogWarning(
                "[MouseBoot] GameDeviceFrame not found. " +
                "Using PlayArea overlay.", this);
        }

        blackTop = CreateSolid("ScreenBlack_Top", overlayRoot, Color.black);
        blackBottom = CreateSolid("ScreenBlack_Bottom", overlayRoot, Color.black);

        centerLine = CreateSolid("PowerLine", overlayRoot, powerLineColor);
        centerLineImage = centerLine.GetComponent<Image>();
        ConfigureHorizontalLine(centerLine, centerLineThickness);
        centerLine.localScale = new Vector3(0f, 1f, 1f);

        topEdge = CreateSolid("PowerEdge_Top", overlayRoot, powerLineColor);
        topEdgeImage = topEdge.GetComponent<Image>();
        ConfigureHorizontalLine(topEdge, openingEdgeThickness);

        bottomEdge = CreateSolid("PowerEdge_Bottom", overlayRoot, powerLineColor);
        bottomEdgeImage = bottomEdge.GetComponent<Image>();
        ConfigureHorizontalLine(bottomEdge, openingEdgeThickness);
    }

    private void RemoveOverlay()
    {
        if (overlayRoot == null)
            return;

        Destroy(overlayRoot.gameObject);
        overlayRoot = null;
        blackTop = null;
        blackBottom = null;
        centerLine = null;
        topEdge = null;
        bottomEdge = null;
        centerLineImage = null;
        topEdgeImage = null;
        bottomEdgeImage = null;
    }

    private void SetOpening(float progress)
    {
        if (blackTop == null || blackBottom == null)
            return;

        float halfGap = 0.5f * Mathf.Clamp01(progress);
        float upper = 0.5f + halfGap;
        float lower = 0.5f - halfGap;

        blackTop.anchorMin = new Vector2(0f, upper);
        blackTop.anchorMax = Vector2.one;
        blackTop.offsetMin = Vector2.zero;
        blackTop.offsetMax = Vector2.zero;

        blackBottom.anchorMin = Vector2.zero;
        blackBottom.anchorMax = new Vector2(1f, lower);
        blackBottom.offsetMin = Vector2.zero;
        blackBottom.offsetMax = Vector2.zero;

        PositionLine(topEdge, upper);
        PositionLine(bottomEdge, lower);
    }

    private static void PositionLine(RectTransform line, float y)
    {
        if (line == null) return;
        line.anchorMin = new Vector2(0f, y);
        line.anchorMax = new Vector2(1f, y);
        line.anchoredPosition = Vector2.zero;
    }

    private static void ConfigureHorizontalLine(
        RectTransform line, float thickness)
    {
        line.anchorMin = new Vector2(0f, 0.5f);
        line.anchorMax = new Vector2(1f, 0.5f);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, thickness);
    }

    private static RectTransform CreateSolid(
        string objectName, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void CopyPlayAreaRect()
    {
        if (overlayRoot == null || playArea == null)
            return;

        overlayRoot.anchorMin = playArea.anchorMin;
        overlayRoot.anchorMax = playArea.anchorMax;
        overlayRoot.pivot = playArea.pivot;
        overlayRoot.anchoredPosition = playArea.anchoredPosition;
        overlayRoot.sizeDelta = playArea.sizeDelta;
        overlayRoot.localScale = playArea.localScale;
        overlayRoot.localRotation = playArea.localRotation;
    }

    private void SetLineAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = powerLineColor;
        color.a *= Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    private static float SmoothStep(float a, float b, float value)
    {
        float t = Mathf.Clamp01(
            (value - a) / Mathf.Max(0.00001f, b - a));
        return t * t * (3f - 2f * t);
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
            Destroy(overlayRoot.gameObject);
    }
}
