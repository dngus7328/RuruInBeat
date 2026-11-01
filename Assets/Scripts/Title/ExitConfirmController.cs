using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ExitConfirmController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Popup")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text messageText;

    private TitleMenuController titleMenuController;

    private void Awake()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(
                ConfirmExit
            );
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(
                CloseConfirm
            );
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(
                ConfirmExit
            );
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(
                CloseConfirm
            );
        }
    }

    private void Update()
    {
        if (
            confirmPanel == null ||
            !confirmPanel.activeSelf
        )
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseConfirm();
        }
    }

    // =========================================================
    // Open
    // =========================================================

    public void OpenConfirmFromTitle(
        TitleMenuController controller
    )
    {
        titleMenuController =
            controller;

        if (messageText != null)
        {
            messageText.text =
                "정말 게임을 종료하시겠습니까?";
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }

        // 기본 선택 = 아니오
        if (
            EventSystem.current != null &&
            noButton != null
        )
        {
            EventSystem.current.SetSelectedGameObject(
                noButton.gameObject
            );
        }
    }

    // =========================================================
    // Close
    // =========================================================

    private void CloseConfirm()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (titleMenuController != null)
        {
            titleMenuController
                .RestoreAfterExitCancel();
        }

        titleMenuController = null;
    }

    // =========================================================
    // Exit
    // =========================================================

    private void ConfirmExit()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveNow();
        }

#if UNITY_EDITOR

        EditorApplication.isPlaying =
            false;

#else

        Application.Quit();

#endif
    }
}