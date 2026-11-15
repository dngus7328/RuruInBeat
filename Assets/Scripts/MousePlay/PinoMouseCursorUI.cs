using System.Collections;
using LuluInBeat.MouseMode.ChartSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mouse mode Pino cursor and unified pause/resume controller.
/// Attach to GameplayCanvas/PlayArea/PinoMoveArea/Pino.
/// Keeps chart/audio paused until the resume countdown finishes.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class PinoMouseCursorUI : MonoBehaviour
{
    [Header("기존 피노")]
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private RectTransform movementArea;

    [Header("마우스 이동")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 18f;
    [SerializeField, Min(0f)] private float maxMoveSpeed = 900f;

    [Header("일시정지")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private MouseModeChartPlayer chartPlayer;
    // GO gate comes from the existing startup sequence, not a separate timer.
    private MouseBootSequenceUI bootSequence;
    [SerializeField] private bool handleEscape = true;
    [SerializeField] private bool pauseOnFocusLost = true;

    [Header("일시정지 메뉴 애니메이션")]
    [Tooltip("일시정지 메뉴가 나타나는 시간. 음악과 노트는 ESC를 누르는 순간 바로 멈춥니다.")]
    [SerializeField, Range(0.08f, 0.8f)] private float pauseEnterSeconds = 0.26f;
    [Tooltip("재개 버튼을 누른 후 메뉴가 사라지는 시간. 이후 3-2-1-GO가 시작됩니다.")]
    [SerializeField, Range(0.08f, 0.8f)] private float pauseExitSeconds = 0.18f;
    [Tooltip("버튼과 문구가 아래에서 올라오는 거리(px). 프레임과 배경은 움직이지 않습니다.")]
    [SerializeField, Range(0f, 60f)] private float pauseRisePixels = 12f;
    [Tooltip("버튼과 문구의 등장 시작 크기. 프레임 크기는 바뀌지 않습니다.")]
    [SerializeField, Range(0.85f, 1f)] private float pauseStartScale = 0.96f;
    [Tooltip("선택 사항: 버튼·문구를 찾을 메뉴 영역입니다. 비워두면 PausePanel 전체에서 찾습니다. 이 오브젝트 자체나 프레임은 움직이지 않습니다.")]
    [SerializeField] private RectTransform pauseMenuContent;

    [Header("재개 카운트다운")]
    [SerializeField] private TMP_FontAsset resumeFont;
    [SerializeField, Range(36f, 160f)] private float resumeFontSize = 105f;
    [SerializeField, Range(0.3f, 2f)] private float countStepSeconds = 0.85f;
    [SerializeField, Range(0.1f, 1.5f)] private float goHoldSeconds = 0.45f;
    [SerializeField]
    private Color resumeTextColor =
        new Color(0.94f, 0.91f, 1f, 1f);

    [Header("진단")]
    [SerializeField] private bool logPauseEvents = true;

    private enum PauseState
    {
        None,
        Manual,
        FocusLost,
        ResumeCountdown
    }

    private RectTransform pinoRect;
    private Image pinoImage;
    private bool gameplayActive = true;
    private bool hasFocus;
    private PauseState pauseState = PauseState.None;
    private bool pauseBeganDuringGameplay;

    private float savedTimeScale = 1f;
    private bool savedAudioPause;
    private bool ownsGlobalPause;
    private int ignoreMouseFrames;

    private Coroutine resumeRoutine;
    private GameObject resumeVisual;
    private TextMeshProUGUI resumeText;
    private RectTransform resumeRect;
    private CanvasGroup resumeGroup;

    // Keep the device frame AND pause-window frame static.
    // Only text and buttons animate. The root CanvasGroup blocks input, never fades.
    // Everything uses unscaled time so it still animates while gameplay is frozen.
    private CanvasGroup pausePanelGroup;
    private readonly System.Collections.Generic.List<PauseAnimatedItem> pauseItems =
        new System.Collections.Generic.List<PauseAnimatedItem>();
    private float pauseOriginalAlpha = 1f;
    private bool pauseOriginalInteractable = true;
    private bool pauseOriginalBlocksRaycasts = true;
    private float pauseAnimationProgress;
    private bool pauseWantsToShow;
    private bool pauseVisualInitialized;
    private bool warnedExternalPausePanel;

    private sealed class PauseAnimatedItem
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 originalPosition;
        public Vector3 originalScale;
        public float originalAlpha;
        public bool movePosition;
    }

    private bool CanControlGameplay()
    {
        // Keep Pino's original unlock-at-GO behavior unchanged.
        return gameplayActive &&
            (bootSequence == null || bootSequence.IsGameplayStarted);
    }

    private bool CanOpenPauseMenu()
    {
        // Fail closed if the boot controller is missing. Never silently
        // allow a pre-GO ESC or a PauseGame() Unity Button call.
        return gameplayActive && bootSequence != null &&
            bootSequence.IsInitialGoFinished;
    }

    public bool IsPaused { get { return pauseState != PauseState.None; } }
    public bool IsGameplayActive { get { return CanControlGameplay(); } }
    public bool IsResumeCountingDown
    {
        get { return pauseState == PauseState.ResumeCountdown; }
    }

    private void Awake()
    {
        pinoRect = GetComponent<RectTransform>();
        pinoImage = GetComponent<Image>();

        // The boot controller is attached to the parent PlayArea.
        bootSequence = GetComponentInParent<MouseBootSequenceUI>();
        if (bootSequence == null)
            bootSequence = FindObjectOfType<MouseBootSequenceUI>();

        if (bootSequence == null)
            Debug.LogError("[PinoPause] MouseBootSequenceUI not found: ESC pause disabled to protect startup countdown.", this);

        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();

        if (movementArea == null)
            movementArea = transform.parent as RectTransform;

        // MouseBootSequenceUI disables the chart component in its Awake.
        // Look up the active GameObject instead of relying on component enabled state.
        if (chartPlayer == null)
        {
            GameObject chartObject = GameObject.Find("MouseNoteManager");
            if (chartObject != null)
                chartPlayer = chartObject.GetComponent<MouseModeChartPlayer>();
        }

        if (chartPlayer == null)
            chartPlayer = FindObjectOfType<MouseModeChartPlayer>();

        if (sourceRenderer == null || movementArea == null)
        {
            Debug.LogError(
                "PinoMouseCursorUI: Source Renderer 또는 Movement Area 연결이 없습니다.",
                this);
            enabled = false;
            return;
        }

        if (transform.parent != movementArea)
        {
            Debug.LogError(
                "PinoMouseCursorUI: Pino는 PinoMoveArea의 직접 자식이어야 합니다.",
                this);
            enabled = false;
            return;
        }

        pinoRect.anchorMin = new Vector2(0.5f, 0.5f);
        pinoRect.anchorMax = new Vector2(0.5f, 0.5f);
        pinoRect.pivot = new Vector2(0.5f, 0.5f);

        pinoImage.raycastTarget = false;
        pinoImage.preserveAspect = true;
        pinoImage.enabled = true;
        sourceRenderer.enabled = false;
    }

    private void Start()
    {
        hasFocus = Application.isFocused;

        PreparePauseVisual();
        HidePauseVisualImmediately();

        ignoreMouseFrames = 2;
        ClampPosition();
        ApplyCursorState();
    }

    private void Update()
    {
        if (Application.isFocused != hasFocus)
            HandleFocusChange(Application.isFocused);

        if (handleEscape && CanOpenPauseMenu() && hasFocus &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseState == PauseState.None)
                PauseGame();
            else if (pauseState == PauseState.Manual)
                ResumeGame();

            // During the resume countdown, ESC is ignored.
            return;
        }

        if (!CanControlGameplay() || IsPaused || !hasFocus)
            return;

        if (ignoreMouseFrames > 0)
        {
            ignoreMouseFrames--;
            return;
        }

        MovePino();
    }

    private void LateUpdate()
    {
        if (sourceRenderer != null && pinoImage != null &&
            pinoImage.sprite != sourceRenderer.sprite)
        {
            pinoImage.sprite = sourceRenderer.sprite;
        }

        ApplyCursorState();
        UpdatePauseVisual();

        // The PausePanel must never appear before the initial GO is gone.
        // If another script enables the same panel, identify that explicitly.
        if (pausePanel != null && pausePanel.activeSelf &&
            pauseState == PauseState.None && !CanOpenPauseMenu())
        {
            if (!warnedExternalPausePanel)
            {
                Debug.LogWarning(
                    "[PinoPause] PausePanel was enabled during startup by another component. " +
                    "Check extra ESC/PauseController/PauseManager scripts on this scene.", this);
                warnedExternalPausePanel = true;
            }
            HidePauseVisualImmediately();
        }
    }

    private void MovePino()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 movement = new Vector2(mouseX, mouseY) * mouseSensitivity;
        float maxStep = maxMoveSpeed * Time.unscaledDeltaTime;
        movement = Vector2.ClampMagnitude(movement, maxStep);

        if (movement.sqrMagnitude < 0.000001f)
            return;

        pinoRect.anchoredPosition += movement;
        ClampPosition();
    }

    private void ClampPosition()
    {
        if (movementArea == null || pinoRect == null)
            return;

        Rect area = movementArea.rect;
        float halfWidth = pinoRect.rect.width *
            Mathf.Abs(pinoRect.localScale.x) * 0.5f;
        float halfHeight = pinoRect.rect.height *
            Mathf.Abs(pinoRect.localScale.y) * 0.5f;
        float limitX = Mathf.Max(0f, area.width * 0.5f - halfWidth);
        float limitY = Mathf.Max(0f, area.height * 0.5f - halfHeight);

        Vector2 position = pinoRect.anchoredPosition;
        position.x = Mathf.Clamp(position.x, -limitX, limitX);
        position.y = Mathf.Clamp(position.y, -limitY, limitY);
        pinoRect.anchoredPosition = position;
    }

    /// <summary>ESC or Pause button. Only available after the initial GO.</summary>
    public void PauseGame()
    {
        if (!CanOpenPauseMenu() || pauseState != PauseState.None)
            return;

        BeginPause(PauseState.Manual);
    }

    /// <summary>ESC or Resume button. Starts a real-time 3-2-1-GO countdown.</summary>
    public void ResumeGame()
    {
        if (pauseState != PauseState.Manual)
            return;

        StartResumeCountdown();
    }

    private void BeginPause(PauseState reason)
    {
        if (pauseState != PauseState.None)
            return;

        // Internal safety gate, including any future direct callers.
        if (reason == PauseState.Manual && !CanOpenPauseMenu())
            return;

        pauseBeganDuringGameplay = gameplayActive;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        savedAudioPause = AudioListener.pause;
        ownsGlobalPause = true;
        pauseState = reason;

        // MouseModeChartPlayer.Update observes these values and calls
        // EnterChartPause(), preserving the DSP-timed note/audio position.
        Time.timeScale = 0f;
        AudioListener.pause = true;

        RefreshPausePanel();
        ApplyCursorState();
        LogState("PAUSE " + reason);
    }

    private void StartResumeCountdown()
    {
        if (pauseState != PauseState.Manual &&
            pauseState != PauseState.FocusLost)
            return;

        // Before the initial GO, the original READY/3/2/1 countdown
        // is already frozen; do not overlay a second countdown.
        if (!pauseBeganDuringGameplay || !CanOpenPauseMenu())
        {
            // Focus loss during READY/initial GO must resume the original
            // startup sequence, not start a second 3-2-1-GO overlay.
            FinishPauseImmediately();
            return;
        }

        pauseState = PauseState.ResumeCountdown;
        RefreshPausePanel();
        ApplyCursorState();

        EnsureResumeVisual();
        if (resumeVisual == null)
        {
            // If TMP UI creation fails, don't strand the game at timeScale=0.
            FinishPauseImmediately();
            return;
        }

        if (resumeRoutine != null)
            StopCoroutine(resumeRoutine);

        // First let the pause menu disappear. No song/audio time advances here.
        resumeRoutine = StartCoroutine(ResumeAfterPauseExitRoutine());
        LogState("COUNTDOWN REQUESTED");
    }

    private IEnumerator ResumeAfterPauseExitRoutine()
    {
        // Do not show the 3 countdown on top of a still-visible pause panel.
        while (pauseState == PauseState.ResumeCountdown &&
               pausePanel != null && pausePanel.activeSelf)
            yield return null;

        if (pauseState != PauseState.ResumeCountdown || !gameplayActive)
        {
            resumeRoutine = null;
            yield break;
        }

        if (resumeVisual == null)
        {
            resumeRoutine = null;
            FinishPauseImmediately();
            yield break;
        }

        resumeVisual.SetActive(true);
        LogState("COUNTDOWN START");
        yield return ResumeCountdownRoutine();
    }

    private IEnumerator ResumeCountdownRoutine()
    {
        string[] steps = { "3", "2", "1", "GO!" };
        int step = 0;
        float elapsed = 0f;
        bool needRestartAfterFocus = false;

        // Keep the chart, audio and gameplay frozen throughout this routine.
        // Time.unscaledDeltaTime advances even with timeScale=0.
        while (step < steps.Length)
        {
            if (!Application.isFocused)
            {
                needRestartAfterFocus = true;
                if (resumeGroup != null)
                    resumeGroup.alpha = 0f;
                yield return null;
                continue;
            }

            // A focus loss during the countdown restarts from 3,
            // so the user always gets the full warning period.
            if (needRestartAfterFocus)
            {
                step = 0;
                elapsed = 0f;
                needRestartAfterFocus = false;
            }

            float duration = step == 3
                ? goHoldSeconds
                : countStepSeconds;

            resumeText.text = steps[step];
            float progress = Mathf.Clamp01(elapsed / duration);

            // Soft, readable pop without glow or decorative effects.
            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress / 0.22f), 3f);
            resumeRect.localScale = Vector3.one * Mathf.Lerp(1.16f, 1f, pop);
            float fadeIn = Mathf.Clamp01(progress / 0.10f);
            float fadeOut = 1f - Mathf.Clamp01((progress - 0.80f) / 0.20f);
            if (step == 3)
                fadeOut = 1f - Mathf.Clamp01((progress - 0.65f) / 0.35f);
            resumeGroup.alpha = fadeIn * fadeOut;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= duration)
            {
                elapsed = 0f;
                step++;
            }

            yield return null;
        }

        // Do not resume on an unfocused frame, even if the last
        // countdown frame coincides with an Editor focus change.
        if (!Application.isFocused)
        {
            resumeRoutine = null;
            StartResumeCountdownFromRestart();
            yield break;
        }

        resumeRoutine = null;
        FinishPauseImmediately();
    }

    private void StartResumeCountdownFromRestart()
    {
        // Preserve the frozen state; a coroutine restarted when focus returns.
        StartCoroutine(RestartCountdownWhenFocused());
    }

    private IEnumerator RestartCountdownWhenFocused()
    {
        while (!Application.isFocused)
            yield return null;

        if (pauseState == PauseState.ResumeCountdown && gameplayActive)
            resumeRoutine = StartCoroutine(ResumeCountdownRoutine());
    }

    private void EnsureResumeVisual()
    {
        if (resumeVisual != null)
            return;

        Transform playArea = movementArea != null
            ? movementArea.parent
            : null;

        if (playArea == null)
        {
            Debug.LogError(
                "[PinoPause] PlayArea를 찾지 못했습니다. Resume countdown UI를 표시할 수 없습니다.",
                this);
            return;
        }

        resumeVisual = new GameObject(
            "ResumeCountdown_Runtime",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup));

        resumeRect = resumeVisual.GetComponent<RectTransform>();
        resumeRect.SetParent(playArea, false);
        resumeRect.anchorMin = new Vector2(0.5f, 0.5f);
        resumeRect.anchorMax = new Vector2(0.5f, 0.5f);
        resumeRect.pivot = new Vector2(0.5f, 0.5f);
        resumeRect.anchoredPosition = Vector2.zero;
        resumeRect.sizeDelta = new Vector2(480f, 156f);
        resumeRect.SetAsLastSibling();

        resumeText = resumeVisual.GetComponent<TextMeshProUGUI>();
        if (resumeFont != null)
            resumeText.font = resumeFont;
        resumeText.text = "3";
        resumeText.fontSize = resumeFontSize;
        resumeText.fontStyle = FontStyles.Bold;
        resumeText.alignment = TextAlignmentOptions.Center;
        resumeText.enableWordWrapping = false;
        resumeText.raycastTarget = false;
        resumeText.color = resumeTextColor;
        resumeText.outlineColor = new Color(0.13f, 0.10f, 0.21f, 0.96f);
        resumeText.outlineWidth = 0.08f;

        resumeGroup = resumeVisual.GetComponent<CanvasGroup>();
        resumeGroup.alpha = 0f;
        resumeGroup.interactable = false;
        resumeGroup.blocksRaycasts = false;
        resumeVisual.SetActive(false);
    }

    private void FinishPauseImmediately()
    {
        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
            resumeRoutine = null;
        }

        if (resumeVisual != null)
            resumeVisual.SetActive(false);

        pauseState = PauseState.None;

        if (ownsGlobalPause)
        {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
            ownsGlobalPause = false;
        }

        ignoreMouseFrames = 2;
        RefreshPausePanel();
        ApplyCursorState();
        LogState("RESUMED");
    }

    public void ExitGameplay()
    {
        // Called by MouseBootSequenceUI on boot/shutdown.
        // Never strand the game in a frozen state while exiting.
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        gameplayActive = false;
        RefreshPausePanel();
        ApplyCursorState();
    }

    public void EnterGameplay()
    {
        // Do not allow external startup calls to bypass READY/3/2/1.
        if (bootSequence != null && !bootSequence.IsGameplayStarted)
        {
            gameplayActive = false;
            RefreshPausePanel();
            ApplyCursorState();
            return;
        }

        gameplayActive = true;
        ignoreMouseFrames = 2;
        hasFocus = Application.isFocused;

        if (!hasFocus && pauseOnFocusLost &&
            pauseState == PauseState.None)
        {
            BeginPause(PauseState.FocusLost);
        }

        RefreshPausePanel();
        ApplyCursorState();
    }

    private void HandleFocusChange(bool focused)
    {
        if (hasFocus == focused)
            return;

        hasFocus = focused;
        ignoreMouseFrames = 2;

        if (!focused)
        {
            if (pauseState == PauseState.None)
                TryPauseForFocusLoss();
            // During ResumeCountdown, the coroutine freezes and
            // restarts its display on focus return.
        }
        else if (pauseState == PauseState.FocusLost)
        {
            StartResumeCountdown();
        }

        RefreshPausePanel();
        ApplyCursorState();
        LogState(focused ? "FOCUS RETURN" : "FOCUS LOST");
    }

    private void TryPauseForFocusLoss()
    {
        if (!pauseOnFocusLost || pauseState != PauseState.None)
            return;

        // The chart starts during the initial READY countdown, whereas
        // player input/ESC starts only on the initial GO.
        bool chartCountdownActive =
            chartPlayer != null && chartPlayer.isActiveAndEnabled;

        if (!gameplayActive && !chartCountdownActive)
            return;

        BeginPause(PauseState.FocusLost);
    }

    private void OnApplicationFocus(bool focused)
    {
        HandleFocusChange(focused && Application.isFocused);
    }

    private void RefreshPausePanel()
    {
        if (pausePanel == null)
            return;

        PreparePauseVisual();
        pauseWantsToShow = CanOpenPauseMenu() &&
            (pauseState == PauseState.Manual ||
             pauseState == PauseState.FocusLost);

        if (pauseWantsToShow && !pausePanel.activeSelf)
        {
            // Start from the current progress if pause was reopened mid-exit.
            pausePanel.SetActive(true);
            ApplyPauseVisual();
        }
        else if (!pauseWantsToShow && pauseAnimationProgress <= 0f)
        {
            pausePanel.SetActive(false);
        }
    }

    private void PreparePauseVisual()
    {
        if (pausePanel == null || pauseVisualInitialized)
            return;

        pausePanelGroup = pausePanel.GetComponent<CanvasGroup>();
        if (pausePanelGroup == null)
            pausePanelGroup = pausePanel.AddComponent<CanvasGroup>();

        pauseOriginalAlpha = pausePanelGroup.alpha;
        pauseOriginalInteractable = pausePanelGroup.interactable;
        pauseOriginalBlocksRaycasts = pausePanelGroup.blocksRaycasts;

        // Do not animate the whole PausePanel, menu window, frame or backdrop.
        // Even if Pause Menu Content points to the frame, scan only its children.
        RectTransform scope = pauseMenuContent;
        if (scope == null || !scope.IsChildOf(pausePanel.transform))
            scope = pausePanel.transform as RectTransform;

        pauseItems.Clear();
        if (scope != null)
        {
            // Buttons animate as a single element including their own labels/icons.
            foreach (Button button in scope.GetComponentsInChildren<Button>(true))
                RegisterPauseItem(button.transform as RectTransform);

            // Non-button text (e.g. PAUSED title) animates independently.
            foreach (TextMeshProUGUI label in scope.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.GetComponentInParent<Button>() == null)
                    RegisterPauseItem(label.transform as RectTransform);

            foreach (Text label in scope.GetComponentsInChildren<Text>(true))
                if (label.GetComponentInParent<Button>() == null)
                    RegisterPauseItem(label.transform as RectTransform);
        }

        pauseAnimationProgress = 0f;
        pauseWantsToShow = false;
        pauseVisualInitialized = true;
        ApplyPauseVisual();
    }

    private void RegisterPauseItem(RectTransform rect)
    {
        if (rect == null || rect.gameObject == pausePanel)
            return;
        foreach (PauseAnimatedItem item in pauseItems)
            if (item.rect == rect)
                return;

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null)
            group = rect.gameObject.AddComponent<CanvasGroup>();

        RectTransform parentRect = rect.parent as RectTransform;
        // Don't fight VerticalLayoutGroup/HorizontalLayoutGroup/GridLayoutGroup.
        bool canMove = parentRect != null && parentRect.GetComponent<LayoutGroup>() == null;
        pauseItems.Add(new PauseAnimatedItem
        {
            rect = rect,
            group = group,
            originalPosition = rect.anchoredPosition,
            originalScale = rect.localScale,
            originalAlpha = group.alpha,
            movePosition = canMove
        });
    }

    private void UpdatePauseVisual()
    {
        if (pausePanel == null || !pauseVisualInitialized)
            return;

        float destination = pauseWantsToShow ? 1f : 0f;
        float duration = pauseWantsToShow
            ? Mathf.Max(0.01f, pauseEnterSeconds)
            : Mathf.Max(0.01f, pauseExitSeconds);

        pauseAnimationProgress = Mathf.MoveTowards(
            pauseAnimationProgress,
            destination,
            Time.unscaledDeltaTime / duration);

        ApplyPauseVisual();

        if (!pauseWantsToShow && pauseAnimationProgress <= 0f &&
            pausePanel.activeSelf)
            pausePanel.SetActive(false);
    }

    private void ApplyPauseVisual()
    {
        if (!pauseVisualInitialized || pausePanelGroup == null)
            return;

        float t = Mathf.Clamp01(pauseAnimationProgress);
        float smooth = t * t * (3f - 2f * t);

        // IMPORTANT: root alpha stays fixed. This protects any existing
        // GameDeviceFrame, PausePanel background, and menu-window frame.
        pausePanelGroup.alpha = pauseOriginalAlpha;

        bool canClick = pauseWantsToShow && t >= 0.99f;
        pausePanelGroup.interactable = pauseOriginalInteractable && canClick;
        pausePanelGroup.blocksRaycasts = pauseOriginalBlocksRaycasts && canClick;

        foreach (PauseAnimatedItem item in pauseItems)
        {
            if (item.rect == null || item.group == null)
                continue;

            // Only foreground controls/titles fade and pop in.
            item.group.alpha = item.originalAlpha * smooth;
            item.rect.localScale = item.originalScale *
                Mathf.Lerp(pauseStartScale, 1f, smooth);
            if (item.movePosition)
                item.rect.anchoredPosition = item.originalPosition +
                    Vector2.down * (Mathf.Max(0f, pauseRisePixels) * (1f - smooth));
        }
    }

    private void HidePauseVisualImmediately()
    {
        if (pausePanel == null)
            return;

        pauseWantsToShow = false;
        pauseAnimationProgress = 0f;
        if (pauseVisualInitialized && pausePanelGroup != null)
        {
            pausePanelGroup.alpha = pauseOriginalAlpha;
            pausePanelGroup.interactable = pauseOriginalInteractable;
            pausePanelGroup.blocksRaycasts = pauseOriginalBlocksRaycasts;
        }
        foreach (PauseAnimatedItem item in pauseItems)
        {
            if (item.rect != null)
            {
                item.rect.anchoredPosition = item.originalPosition;
                item.rect.localScale = item.originalScale;
            }
            if (item.group != null)
                item.group.alpha = item.originalAlpha;
        }
        pausePanel.SetActive(false);
    }

    private void ApplyCursorState()
    {
        bool playing = isActiveAndEnabled && CanControlGameplay() &&
            pauseState == PauseState.None && hasFocus;

        CursorLockMode target = playing
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        if (Cursor.lockState != target)
            Cursor.lockState = target;

        Cursor.visible = !playing;
    }

    private void LogState(string action)
    {
        if (!logPauseEvents)
            return;

        Debug.Log(
            "[PinoPause] " + action +
            " | focus=" + Application.isFocused +
            " | gameplay=" + gameplayActive +
            " | reason=" + pauseState +
            " | timeScale=" + Time.timeScale +
            " | audioPause=" + AudioListener.pause,
            this);
    }

    private void OnDisable()
    {
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        HidePauseVisualImmediately();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        HidePauseVisualImmediately();
        if (resumeVisual != null)
            Destroy(resumeVisual);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
