using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Resume Countdown - World TMP")]
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("2D 월드 기준 카운트다운 Z 위치")]
    [SerializeField] private float countdownWorldZ = 0f;

    [SerializeField] private string countdownSortingLayer = "Default";
    [SerializeField] private int countdownSortingOrder = 5000;

    private bool isPaused;
    private bool isCountingDown;

    public bool IsPaused => isPaused;

    // 모든 게임플레이 입력 스크립트가 이 값만 확인한다.
    public static bool GameplayInputBlocked { get; private set; }

    private void Awake()
    {
        GameplayInputBlocked = false;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        PrepareCountdownWorldText();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        isPaused = false;
        isCountingDown = false;
        GameplayInputBlocked = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (!isPaused)
        {
            PauseGame();
            return;
        }

        if (isCountingDown)
        {
            CancelCountdown();
            return;
        }

        StartResumeCountdown();
    }

    // =========================================================
    // Pause
    // =========================================================

    public void PauseGame()
    {
        if (isPaused)
            return;

        isPaused = true;
        GameplayInputBlocked = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    // =========================================================
    // Resume
    // =========================================================

    public void StartResumeCountdown()
    {
        if (!isPaused || isCountingDown)
            return;

        StartCoroutine(ResumeCountdownRoutine());
    }

    private IEnumerator ResumeCountdownRoutine()
    {
        isCountingDown = true;
        GameplayInputBlocked = true;

        // 카운트다운 동안에도 게임/음악은 정지
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        PrepareCountdownWorldText();
        PlaceCountdownAtCameraCenter();

        if (countdownRoot != null)
            countdownRoot.SetActive(true);

        SetCountdownText("3");
        yield return new WaitForSecondsRealtime(1f);

        SetCountdownText("2");
        yield return new WaitForSecondsRealtime(1f);

        SetCountdownText("1");
        yield return new WaitForSecondsRealtime(1f);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        isCountingDown = false;
        isPaused = false;

        // 1이 끝난 뒤에만 입력/시간/음악을 동시에 재개
        GameplayInputBlocked = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    // =========================================================
    // Countdown
    // =========================================================

    private void SetCountdownText(string value)
    {
        if (countdownText == null)
            return;

        countdownText.text = value;
        countdownText.alpha = 1f;
        countdownText.gameObject.SetActive(true);
        countdownText.ForceMeshUpdate();
    }

    private void PrepareCountdownWorldText()
    {
        if (countdownRoot == null || countdownText == null)
            return;

        // PausePanel 자식이면 패널 OFF와 함께 사라지므로 형제로 이동
        if (pausePanel != null &&
            countdownRoot.transform.IsChildOf(pausePanel.transform))
        {
            Transform newParent = pausePanel.transform.parent;
            countdownRoot.transform.SetParent(newParent, true);
        }

        Renderer textRenderer =
            countdownText.GetComponent<Renderer>();

        if (textRenderer != null)
        {
            textRenderer.sortingLayerID =
                SortingLayer.NameToID(countdownSortingLayer);

            textRenderer.sortingOrder =
                countdownSortingOrder;

            textRenderer.enabled = true;
        }
    }

    private void PlaceCountdownAtCameraCenter()
    {
        if (countdownRoot == null)
            return;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
            return;

        Vector3 cameraPosition =
            gameplayCamera.transform.position;

        countdownRoot.transform.position =
            new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                countdownWorldZ
            );

        countdownRoot.transform.rotation =
            Quaternion.identity;
    }

    private void CancelCountdown()
    {
        StopAllCoroutines();

        isCountingDown = false;
        GameplayInputBlocked = true;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();

        GameplayInputBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
