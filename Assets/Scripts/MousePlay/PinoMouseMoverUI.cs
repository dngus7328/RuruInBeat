
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class PinoMouseMoverUI : MonoBehaviour
{
    [Header("기존 피노 애니메이션")]
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Header("피노 이동 영역")]
    [SerializeField] private RectTransform movementArea;

    [Header("마우스 이동")]
    [SerializeField] private float mouseSensitivity = 18f;
    [SerializeField] private float maxMoveSpeed = 900f;

    [Header("일시정지")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private bool handleEscapeKey = true;
    [SerializeField] private bool pauseOnFocusLost = true;

    private RectTransform pinoRect;
    private Image pinoImage;

    private bool gameplayActive = true;
    private bool isPaused = false;
    private bool ownsPause = false;

    private float previousTimeScale = 1f;
    private bool previousAudioPause;

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

        if (movementArea == null ||
            transform.parent != movementArea)
        {
            Debug.LogError(
                "Pino의 Movement Area를 연결하세요.",
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

        // 기존 PinoSpriteAnimator는 계속 작동한다.
        // 화면에는 UI Image로만 표시하여 중복 출력을 막는다.
        if (sourceRenderer != null)
            sourceRenderer.enabled = false;
    }

    private void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        ClampPosition();
        UpdateCursor();
    }

    private void Update()
    {
        // 다른 창으로 나갔는데 포커스 콜백이
        // 누락된 경우까지 확인한다.
        if (pauseOnFocusLost &&
            gameplayActive &&
            !isPaused &&
            !Application.isFocused)
        {
            PauseGame();
        }

        if (handleEscapeKey &&
            gameplayActive &&
            Application.isFocused &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        if (!gameplayActive ||
            isPaused ||
            !Application.isFocused)
        {
            return;
        }

        MovePino();
    }

    private void LateUpdate()
    {
        // 기존 피노 애니메이션에서 현재 스프라이트 복사
        if (sourceRenderer != null &&
            pinoImage != null &&
            pinoImage.sprite != sourceRenderer.sprite)
        {
            pinoImage.sprite = sourceRenderer.sprite;
        }

        // 창 전환 후 커서 상태가 풀려도 다시 적용
        UpdateCursor();
    }

    private void MovePino()
    {
        if (movementArea == null)
            return;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 delta = new Vector2(
            mouseX,
            mouseY
        ) * mouseSensitivity;

        float maxStep = maxMoveSpeed * Time.deltaTime;

        delta = Vector2.ClampMagnitude(delta, maxStep);

        pinoRect.anchoredPosition += delta;

        ClampPosition();
    }

    private void ClampPosition()
    {
        if (movementArea == null)
            return;

        // 부모 Pivot 위치에 영향을 받지 않도록
        // 중앙 Anchor 기준으로 이동 범위를 계산한다.
        Rect area = movementArea.rect;

        float halfAreaWidth = area.width * 0.5f;
        float halfAreaHeight = area.height * 0.5f;

        float halfPinoWidth =
            pinoRect.rect.width *
            Mathf.Abs(pinoRect.localScale.x) * 0.5f;

        float halfPinoHeight =
            pinoRect.rect.height *
            Mathf.Abs(pinoRect.localScale.y) * 0.5f;

        float limitX = Mathf.Max(
            0f,
            halfAreaWidth - halfPinoWidth
        );

        float limitY = Mathf.Max(
            0f,
            halfAreaHeight - halfPinoHeight
        );

        Vector2 position = pinoRect.anchoredPosition;

        position.x = Mathf.Clamp(
            position.x, -limitX, limitX
        );

        position.y = Mathf.Clamp(
            position.y, -limitY, limitY
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

        UpdateCursor();
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;

        RestorePause();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        UpdateCursor();
    }

    public void EnterGameplay()
    {
        gameplayActive = true;

        if (pausePanel != null && !isPaused)
            pausePanel.SetActive(false);

        UpdateCursor();
    }

    public void ExitGameplay()
    {
        if (isPaused)
            ResumeGame();

        gameplayActive = false;

        UpdateCursor();
    }

    private void RestorePause()
    {
        if (!ownsPause)
            return;

        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;

        ownsPause = false;
    }

    private void UpdateCursor()
    {
        bool controllingPino =
            isActiveAndEnabled &&
            gameplayActive &&
            !isPaused &&
            Application.isFocused;

        CursorLockMode desiredLock = controllingPino
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        if (Cursor.lockState != desiredLock)
            Cursor.lockState = desiredLock;

        Cursor.visible = !controllingPino;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused &&
            gameplayActive &&
            !isPaused &&
            pauseOnFocusLost)
        {
            PauseGame();
        }

        // 포커스가 돌아와도 일시정지는 유지.
        // ESC 또는 Resume 버튼으로 해제한다.
        UpdateCursor();
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
