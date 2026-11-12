
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class MouseFocusResumeFix : MonoBehaviour
{
    [Header("기존 일시정지 담당")]
    [Tooltip("PinoMouseCursorUI가 붙어 있는 Pino 오브젝트")]
    [SerializeField] private GameObject pauseController;

    [Header("일시정지 패널")]
    [SerializeField] private GameObject pausePanel;

    private bool wasPlayingBeforeFocusLoss;
    private bool focusWasLost;
    private bool autoResumePending;

    private void Awake()
    {
        wasPlayingBeforeFocusLoss =
            Time.timeScale > 0f &&
            !AudioListener.pause;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            if (focusWasLost)
                return;

            focusWasLost = true;

            // 포커스를 잃기 전 정상 플레이 중이었을 때만
            // 돌아온 뒤 자동 재개할 수 있도록 기록.
            autoResumePending =
                wasPlayingBeforeFocusLoss;

            return;
        }

        if (!focusWasLost)
            return;

        focusWasLost = false;

        // 자동 재개는 LateUpdate에서 실행.
        // 다른 스크립트의 포커스 복귀 처리가
        // 먼저 끝나도록 한다.
    }

    private void LateUpdate()
    {
        if (!Application.isFocused)
            return;

        if (autoResumePending && !focusWasLost)
        {
            autoResumePending = false;

            ResumeThroughController();
        }

        // 정상 플레이 상태인지 기억한다.
        // ESC로 사용자가 직접 일시정지했다면 false.
        wasPlayingBeforeFocusLoss =
            Time.timeScale > 0f &&
            !AudioListener.pause &&
            (pausePanel == null ||
             !pausePanel.activeSelf);
    }

    private void ResumeThroughController()
    {
        if (pauseController == null)
        {
            Debug.LogError(
                "[MouseFocusResumeFix] " +
                "Pause Controller를 연결해야 합니다.",
                this
            );
            return;
        }

        // Time.timeScale만 강제로 변경하지 않는다.
        // 기존 일시정지 담당이 직접 재개하게 한다.
        // 해당 스크립트에 ResumeGame() 메서드가
        // 있어야 한다.
        pauseController.SendMessage(
            "ResumeGame",
            SendMessageOptions.RequireReceiver
        );

        Debug.Log(
            "[MouseFocusResumeFix] " +
            "포커스 복귀: 기존 ResumeGame 호출",
            this
        );
    }
}
