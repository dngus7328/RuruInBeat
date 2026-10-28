using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Resume Countdown")]
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text countdownText;

    private bool isPaused;
    private bool isCountingDown;

    public bool IsPaused => isPaused;

    // RhythmManager 등에서 참조하는 값
    // 게임 시작 시에는 반드시 false
    public static bool GameplayInputBlocked { get; private set; }

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // 시작부터 입력 막히는 문제 방지
        GameplayInputBlocked = false;
    }

    private void Start()
    {
        isPaused = false;
        isCountingDown = false;

        GameplayInputBlocked = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

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

        // 게임 중 ESC
        if (!isPaused)
        {
            PauseGame();
            return;
        }

        // 카운트다운 중 ESC
        if (isCountingDown)
        {
            CancelCountdown();
            return;
        }

        // 일시정지 중 ESC
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

        // 여기서부터 게임플레이 입력만 차단
        GameplayInputBlocked = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);
    }

    // =========================================================
    // Resume
    // =========================================================

    public void StartResumeCountdown()
    {
        if (!isPaused || isCountingDown)
            return;

        StartCoroutine(
            ResumeCountdown()
        );
    }

    private IEnumerator ResumeCountdown()
    {
        isCountingDown = true;

        // 3,2,1 동안에도 입력은 차단
        GameplayInputBlocked = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (countdownRoot != null)
            countdownRoot.SetActive(true);

        if (countdownText != null)
            countdownText.text = "3";

        yield return
            new WaitForSecondsRealtime(1f);

        if (countdownText != null)
            countdownText.text = "2";

        yield return
            new WaitForSecondsRealtime(1f);

        if (countdownText != null)
            countdownText.text = "1";

        yield return
            new WaitForSecondsRealtime(1f);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        isCountingDown = false;
        isPaused = false;

        // 카운트다운이 완전히 끝난 뒤 입력 복구
        GameplayInputBlocked = false;

        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    // =========================================================
    // Countdown Cancel
    // =========================================================

    private void CancelCountdown()
    {
        StopAllCoroutines();

        isCountingDown = false;

        // 다시 Pause 상태이므로 계속 차단
        GameplayInputBlocked = true;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    // =========================================================
    // Safety
    // =========================================================

    private void OnDestroy()
    {
        GameplayInputBlocked = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}