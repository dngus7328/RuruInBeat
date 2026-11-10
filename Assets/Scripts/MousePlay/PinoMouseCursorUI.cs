
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
    [Min(0f)]
    [SerializeField] private float mouseSensitivity = 18f;

    [Min(0f)]
    [SerializeField] private float maxMoveSpeed = 900f;

    [Header("일시정지")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private bool handleEscape = true;
    [SerializeField] private bool pauseOnFocusLost = true;

    private RectTransform pinoRect;
    private Image pinoImage;

    private bool gameplayActive = true;
    private bool isPaused = false;
    private bool hasFocus = true;

    private bool ownsPause = false;
    private float previousTimeScale = 1f;
    private bool previousAudioPause = false;

    private int ignoreMouseFrames = 0;

    public bool IsPaused => isPaused;
    public bool IsGameplayActive => gameplayActive;

    private void Awake()
    {
        pinoRect = GetComponent<RectTransform>();
        pinoImage = GetComponent<Image>();

        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();

        if (movementArea == null)
            movementArea = transform.parent as RectTransform;

        if (sourceRenderer == null || movementArea == null)
        {
            Debug.LogError(
                "Pino의 Source Renderer 또는 Movement Area가 없습니다.",
                this
            );

            enabled = false;
            return;
        }

        if (transform.parent != movementArea)
        {
            Debug.LogError(
                "Pino는 PinoMoveArea의 직접 자식이어야 합니다.",
                this
            );

            enabled = false;
            return;
        }

        pinoRect.anchorMin = new Vector2(0.5f, 0.5f);
        pinoRect.anchorMax = new Vector2(0.5f, 0.5f);
        pinoRect.pivot = new Vector2(0.5f, 0.5f);

        pinoImage.raycastTarget = false;
        pinoImage.preserveAspect = true;
        pinoImage.enabled = true;

        // 기존 PinoSpriteAnimator가 SpriteRenderer.sprite를
        // 갱신하고, 화면 출력은 UI Image가 담당한다.
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
        // 포커스 이벤트가 누락된 경우도 확인
        if (Application.isFocused != hasFocus)
            HandleFocusChange(Application.isFocused);

        if (handleEscape &&
            gameplayActive &&
            hasFocus &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();

            return;
        }

        if (!gameplayActive || isPaused || !hasFocus)
            return;

        // 재개 직후 마우스 이동량 무시
        if (ignoreMouseFrames > 0)
        {
            ignoreMouseFrames--;
            return;
        }

        MovePino();
    }

    private void LateUpdate()
    {
        // 기존 애니메이션 프레임을 UI Image에 표시
        if (sourceRenderer != null &&
            pinoImage != null &&
            pinoImage.sprite != sourceRenderer.sprite)
        {
            pinoImage.sprite = sourceRenderer.sprite;
        }

        // Unity Editor가 커서 잠금을 해제한 경우 복구
        ApplyCursorState();
    }

    private void MovePino()
    {
        // 마우스 위치가 아니라 이동량 사용
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 movement = new Vector2(
            mouseX,
            mouseY
        ) * mouseSensitivity;

        // Time.timeScale에 의존하지 않는 이동 시간
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
        if (movementArea == null)
            return;

        Rect area = movementArea.rect;

        float halfWidth =
            pinoRect.rect.width *
            Mathf.Abs(pinoRect.localScale.x) * 0.5f;

        float halfHeight =
            pinoRect.rect.height *
            Mathf.Abs(pinoRect.localScale.y) * 0.5f;

        // 중앙 Anchor 기준의 이동 가능 범위
        float limitX = Mathf.Max(
            0f,
            area.width * 0.5f - halfWidth
        );

        float limitY = Mathf.Max(
            0f,
            area.height * 0.5f - halfHeight
        );

        Vector2 position = pinoRect.anchoredPosition;

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
        if (isPaused || !gameplayActive)
            return;

        isPaused = true;

        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;

        ownsPause = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        ApplyCursorState();
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;

        RestorePause();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        // 커서를 다시 잠글 때 발생하는 이동량 방지
        ignoreMouseFrames = 2;

        ApplyCursorState();
    }

    public void ExitGameplay()
    {
        if (isPaused)
            ResumeGame();

        gameplayActive = false;

        ApplyCursorState();
    }

    public void EnterGameplay()
    {
        gameplayActive = true;
        ignoreMouseFrames = 2;

        ApplyCursorState();
    }

    private void HandleFocusChange(bool focused)
    {
        if (hasFocus == focused)
            return;

        hasFocus = focused;

        if (!focused)
        {
            if (pauseOnFocusLost &&
                gameplayActive &&
                !isPaused)
            {
                PauseGame();
            }
        }
        else
        {
            // 포커스 복구 후에도 일시정지는 유지
            ignoreMouseFrames = 2;
        }

        ApplyCursorState();
    }

    private void OnApplicationFocus(bool focused)
    {
        HandleFocusChange(focused);
    }

    private void ApplyCursorState()
    {
        bool playing =
            isActiveAndEnabled &&
            gameplayActive &&
            !isPaused &&
            hasFocus;

        CursorLockMode targetMode = playing
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        if (Cursor.lockState != targetMode)
            Cursor.lockState = targetMode;

        Cursor.visible = !playing;
    }

    private void RestorePause()
    {
        if (!ownsPause)
            return;

        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;

        ownsPause = false;
    }

    private void OnDisable()
    {
        RestorePause();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        RestorePause();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
