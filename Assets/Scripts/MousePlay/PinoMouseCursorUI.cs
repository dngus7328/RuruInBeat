
using System.Collections;
using LuluInBeat.MouseMode.ChartSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField, Min(0f)]
    private float mouseSensitivity = 18f;

    [SerializeField, Min(0f)]
    private float maxMoveSpeed = 900f;

    [Header("일시정지")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private MouseModeChartPlayer chartPlayer;
    [SerializeField] private bool handleEscape = true;
    [SerializeField] private bool pauseOnFocusLost = true;

    [Header("재개 카운트다운")]
    [SerializeField] private TMP_FontAsset resumeFont;

    [SerializeField, Range(36f, 160f)]
    private float resumeFontSize = 105f;

    [SerializeField, Range(0.3f, 2f)]
    private float countStepSeconds = 0.85f;

    [SerializeField, Range(0.1f, 1.5f)]
    private float goHoldSeconds = 0.45f;

    [SerializeField]
    private Color resumeTextColor =
        new Color(0.94f, 0.91f, 1f, 1f);

    [Header("진단")]
    [SerializeField]
    private bool logPauseEvents = true;

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

    public bool IsPaused
    {
        get { return pauseState != PauseState.None; }
    }

    public bool IsGameplayActive
    {
        get { return gameplayActive; }
    }

    public bool IsResumeCountingDown
    {
        get
        {
            return pauseState ==
                PauseState.ResumeCountdown;
        }
    }

    private void Awake()
    {
        pinoRect = GetComponent<RectTransform>();
        pinoImage = GetComponent<Image>();

        if (sourceRenderer == null)
        {
            sourceRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (movementArea == null)
        {
            movementArea =
                transform.parent as RectTransform;
        }

        if (chartPlayer == null)
        {
            GameObject chartObject =
                GameObject.Find("MouseNoteManager");

            if (chartObject != null)
            {
                chartPlayer =
                    chartObject.GetComponent
                    <MouseModeChartPlayer>();
            }
        }

        if (chartPlayer == null)
        {
            chartPlayer =
                FindObjectOfType<MouseModeChartPlayer>();
        }

        if (sourceRenderer == null ||
            movementArea == null)
        {
            Debug.LogError(
                "PinoMouseCursorUI: " +
                "Source Renderer 또는 " +
                "Movement Area 연결이 없습니다.",
                this
            );

            enabled = false;
            return;
        }

        if (transform.parent != movementArea)
        {
            Debug.LogError(
                "Pino는 PinoMoveArea의 " +
                "직접 자식이어야 합니다.",
                this
            );

            enabled = false;
            return;
        }

        pinoRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        pinoRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        pinoRect.pivot =
            new Vector2(0.5f, 0.5f);

        pinoImage.raycastTarget = false;
        pinoImage.preserveAspect = true;
        pinoImage.enabled = true;

        sourceRenderer.enabled = false;
    }

    private void Start()
    {
        hasFocus = Application.isFocused;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        ignoreMouseFrames = 2;

        ClampPosition();
        ApplyCursorState();
    }

    private void Update()
    {
        if (Application.isFocused != hasFocus)
        {
            HandleFocusChange(
                Application.isFocused
            );
        }

        if (handleEscape &&
            gameplayActive &&
            hasFocus &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseState == PauseState.None)
            {
                PauseGame();
            }
            else if (
                pauseState == PauseState.Manual)
            {
                ResumeGame();
            }

            return;
        }

        if (!gameplayActive ||
            IsPaused ||
            !hasFocus)
        {
            return;
        }

        if (ignoreMouseFrames > 0)
        {
            ignoreMouseFrames--;
            return;
        }

        MovePino();
    }

    private void LateUpdate()
    {
        if (sourceRenderer != null &&
            pinoImage != null &&
            pinoImage.sprite != sourceRenderer.sprite)
        {
            pinoImage.sprite = sourceRenderer.sprite;
        }

        ApplyCursorState();
    }

    private void MovePino()
    {
        float mouseX =
            Input.GetAxisRaw("Mouse X");

        float mouseY =
            Input.GetAxisRaw("Mouse Y");

        Vector2 movement = new Vector2(
            mouseX,
            mouseY
        ) * mouseSensitivity;

        float maxStep =
            maxMoveSpeed * Time.unscaledDeltaTime;

        movement = Vector2.ClampMagnitude(
            movement,
            maxStep
        );

        if (movement.sqrMagnitude < 0.000001f)
            return;

        pinoRect.anchoredPosition += movement;

        ClampPosition();
    }

    private void ClampPosition()
    {
        if (movementArea == null ||
            pinoRect == null)
        {
            return;
        }

        Rect area = movementArea.rect;

        float halfWidth =
            pinoRect.rect.width *
            Mathf.Abs(pinoRect.localScale.x) *
            0.5f;

        float halfHeight =
            pinoRect.rect.height *
            Mathf.Abs(pinoRect.localScale.y) *
            0.5f;

        float limitX = Mathf.Max(
            0f,
            area.width * 0.5f - halfWidth
        );

        float limitY = Mathf.Max(
            0f,
            area.height * 0.5f - halfHeight
        );

        Vector2 position =
            pinoRect.anchoredPosition;

        position.x = Mathf.Clamp(
            position.x,
            -limitX,
            limitX
        );

        position.y = Mathf.Clamp(
            position.y,
            -limitY,
            limitY
        );

        pinoRect.anchoredPosition = position;
    }

    public void PauseGame()
    {
        if (!gameplayActive ||
            pauseState != PauseState.None)
        {
            return;
        }

        BeginPause(PauseState.Manual);
    }

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

        pauseBeganDuringGameplay = gameplayActive;

        savedTimeScale =
            Time.timeScale > 0f
                ? Time.timeScale
                : 1f;

        savedAudioPause = AudioListener.pause;

        ownsGlobalPause = true;
        pauseState = reason;

        // 카운트다운이 끝날 때까지
        // 게임과 오디오를 정지시킨다.
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
        {
            return;
        }

        // 처음 READY 카운트다운 도중이라면
        // 새로운 카운트다운을 겹쳐 띄우지 않는다.
        if (!pauseBeganDuringGameplay ||
            !gameplayActive)
        {
            FinishPauseImmediately();
            return;
        }

        pauseState = PauseState.ResumeCountdown;

        RefreshPausePanel();
        ApplyCursorState();

        EnsureResumeVisual();

        if (resumeVisual == null)
        {
            FinishPauseImmediately();
            return;
        }

        resumeVisual.SetActive(true);

        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
        }

        resumeRoutine = StartCoroutine(
            ResumeCountdownRoutine()
        );

        LogState("COUNTDOWN START");
    }

    private IEnumerator ResumeCountdownRoutine()
    {
        string[] steps =
        {
            "3", "2", "1", "GO!"
        };

        int step = 0;
        float elapsed = 0f;

        bool needRestartAfterFocus = false;

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

            // 재개 카운트다운 중 포커스가
            // 사라졌다면 3부터 다시 시작.
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

            float progress =
                Mathf.Clamp01(elapsed / duration);

            float pop = 1f - Mathf.Pow(
                1f - Mathf.Clamp01(
                    progress / 0.22f
                ),
                3f
            );

            resumeRect.localScale =
                Vector3.one * Mathf.Lerp(
                    1.16f,
                    1f,
                    pop
                );

            float fadeIn =
                Mathf.Clamp01(progress / 0.10f);

            float fadeOut = 1f - Mathf.Clamp01(
                (progress - 0.80f) / 0.20f
            );

            if (step == 3)
            {
                fadeOut = 1f - Mathf.Clamp01(
                    (progress - 0.65f) / 0.35f
                );
            }

            resumeGroup.alpha =
                fadeIn * fadeOut;

            // Time.timeScale이 0이어도
            // 카운트다운만 계속 진행한다.
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= duration)
            {
                elapsed = 0f;
                step++;
            }

            yield return null;
        }

        if (!Application.isFocused)
        {
            resumeRoutine = null;
            StartResumeCountdownFromRestart();
            yield break;
        }

        resumeRoutine = null;

        // GO!까지 완료된 시점에만
        // 실제 게임과 음악을 재개한다.
        FinishPauseImmediately();
    }

    private void StartResumeCountdownFromRestart()
    {
        StartCoroutine(
            RestartCountdownWhenFocused()
        );
    }

    private IEnumerator RestartCountdownWhenFocused()
    {
        while (!Application.isFocused)
            yield return null;

        if (pauseState ==
                PauseState.ResumeCountdown &&
            gameplayActive)
        {
            resumeRoutine = StartCoroutine(
                ResumeCountdownRoutine()
            );
        }
    }

    private void EnsureResumeVisual()
    {
        if (resumeVisual != null)
            return;

        Transform playArea =
            movementArea != null
                ? movementArea.parent
                : null;

        if (playArea == null)
        {
            Debug.LogError(
                "[PinoPause] PlayArea를 찾지 못했습니다.",
                this
            );

            return;
        }

        resumeVisual = new GameObject(
            "ResumeCountdown_Runtime",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup)
        );

        resumeRect =
            resumeVisual.GetComponent<RectTransform>();

        resumeRect.SetParent(playArea, false);

        resumeRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        resumeRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        resumeRect.pivot =
            new Vector2(0.5f, 0.5f);

        resumeRect.anchoredPosition =
            Vector2.zero;

        resumeRect.sizeDelta =
            new Vector2(480f, 156f);

        resumeRect.SetAsLastSibling();

        resumeText =
            resumeVisual.GetComponent<TextMeshProUGUI>();

        if (resumeFont != null)
            resumeText.font = resumeFont;

        resumeText.text = "3";
        resumeText.fontSize = resumeFontSize;
        resumeText.fontStyle = FontStyles.Bold;

        resumeText.alignment =
            TextAlignmentOptions.Center;

        resumeText.enableWordWrapping = false;
        resumeText.raycastTarget = false;
        resumeText.color = resumeTextColor;

        resumeText.outlineColor = new Color(
            0.13f,
            0.10f,
            0.21f,
            0.96f
        );

        resumeText.outlineWidth = 0.08f;

        resumeGroup =
            resumeVisual.GetComponent<CanvasGroup>();

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
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        gameplayActive = false;

        RefreshPausePanel();
        ApplyCursorState();
    }

    public void EnterGameplay()
    {
        gameplayActive = true;
        ignoreMouseFrames = 2;

        hasFocus = Application.isFocused;

        if (!hasFocus &&
            pauseOnFocusLost &&
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
            {
                TryPauseForFocusLoss();
            }
        }
        else if (
            pauseState == PauseState.FocusLost)
        {
            StartResumeCountdown();
        }

        RefreshPausePanel();
        ApplyCursorState();

        LogState(
            focused
                ? "FOCUS RETURN"
                : "FOCUS LOST"
        );
    }

    private void TryPauseForFocusLoss()
    {
        if (!pauseOnFocusLost ||
            pauseState != PauseState.None)
        {
            return;
        }

        bool chartCountdownActive =
            chartPlayer != null &&
            chartPlayer.isActiveAndEnabled;

        if (!gameplayActive &&
            !chartCountdownActive)
        {
            return;
        }

        BeginPause(PauseState.FocusLost);
    }

    private void OnApplicationFocus(bool focused)
    {
        HandleFocusChange(
            focused && Application.isFocused
        );
    }

    private void RefreshPausePanel()
    {
        if (pausePanel == null)
            return;

        bool show =
            gameplayActive &&
            (pauseState == PauseState.Manual ||
             pauseState == PauseState.FocusLost);

        if (pausePanel.activeSelf != show)
            pausePanel.SetActive(show);
    }

    private void ApplyCursorState()
    {
        bool playing =
            isActiveAndEnabled &&
            gameplayActive &&
            pauseState == PauseState.None &&
            hasFocus;

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
            this
        );
    }

    private void OnDisable()
    {
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (pauseState != PauseState.None)
            FinishPauseImmediately();

        if (resumeVisual != null)
            Destroy(resumeVisual);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
