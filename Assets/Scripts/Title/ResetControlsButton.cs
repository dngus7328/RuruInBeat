using UnityEngine;
using UnityEngine.UI;

public class ResetControlsButton : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField]
    private Button resetButton;

    [SerializeField]
    private Button confirmButton;

    [SerializeField]
    private Button cancelButton;

    [Header("Popup")]
    [SerializeField]
    private GameObject confirmPanel;

    private void Awake()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(
                OpenConfirmPanel
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(
                ConfirmReset
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(
                CancelReset
            );
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(
                OpenConfirmPanel
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(
                ConfirmReset
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                CancelReset
            );
        }
    }

    // =========================================================
    // 초기화 버튼
    // =========================================================

    private void OpenConfirmPanel()
    {
        if (confirmPanel == null)
        {
            Debug.LogError(
                "ResetControlsButton: Confirm Panel이 연결되지 않았습니다."
            );

            return;
        }

        confirmPanel.SetActive(true);
    }

    // =========================================================
    // 네
    // =========================================================

    private void ConfirmReset()
    {
        if (InputBindingManager.Instance == null)
        {
            Debug.LogError(
                "ResetControlsButton: InputBindingManager가 없습니다."
            );

            return;
        }

        InputBindingManager.Instance
            .ResetAllBindings();

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // =========================================================
    // 아니오
    // =========================================================

    private void CancelReset()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }
}