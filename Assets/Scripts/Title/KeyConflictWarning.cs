using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyConflictWarning : MonoBehaviour
{
    public static KeyConflictWarning Instance { get; private set; }

    [Header("Popup")]
    [SerializeField]
    private GameObject warningRoot;

    [SerializeField]
    private KeyConflictWarningAnimator warningAnimator;

    [Header("UI")]
    [SerializeField]
    private TMP_Text messageText;

    [SerializeField]
    private Button confirmButton;

    [SerializeField]
    private Button cancelButton;

    private void Awake()
    {
        Instance = this;

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(
                ConfirmSwap
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(
                CancelSwap
            );
        }
    }

    private void OnEnable()
    {
        InputBindingManager.BindingConflictDetected +=
            ShowConflict;
    }

    private void OnDisable()
    {
        InputBindingManager.BindingConflictDetected -=
            ShowConflict;
    }

    private void OnDestroy()
    {
        InputBindingManager.BindingConflictDetected -=
            ShowConflict;

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(
                ConfirmSwap
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                CancelSwap
            );
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ShowConflict(
        string keyName,
        string targetActionName,
        string conflictActionName
    )
    {
        if (messageText != null)
        {
            messageText.text =
                $"{keyName} 키는 이미 " +
                $"{conflictActionName}에서 사용 중입니다.\n" +
                $"{targetActionName}과 키를 서로 교체하시겠습니까?";
        }

        if (warningAnimator != null)
        {
            warningAnimator.Show();
        }
        else if (warningRoot != null)
        {
            warningRoot.SetActive(true);
        }
    }

    private void ConfirmSwap()
    {
        if (InputBindingManager.Instance != null)
        {
            InputBindingManager.Instance
                .ConfirmPendingSwap();
        }

        Hide();
    }

    private void CancelSwap()
    {
        if (InputBindingManager.Instance != null)
        {
            InputBindingManager.Instance
                .CancelPendingConflict();
        }

        Hide();
    }

    private void Hide()
    {
        if (warningAnimator != null)
        {
            warningAnimator.Hide();
        }
        else if (warningRoot != null)
        {
            warningRoot.SetActive(false);
        }
    }
}