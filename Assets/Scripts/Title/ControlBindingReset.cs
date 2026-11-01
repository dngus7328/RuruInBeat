using UnityEngine;

public class ControlBindingReset : MonoBehaviour
{
    [Header("Reset Confirm Panel")]
    [SerializeField]
    private GameObject confirmPanel;

    // 초기화 버튼
    public void OpenConfirm()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    // 네
    public void ConfirmReset()
    {
        if (
            InputBindingManager.Instance !=
            null
        )
        {
            InputBindingManager.Instance
                .ResetAllBindings();
        }

        CloseConfirm();
    }

    // 아니오
    public void CancelReset()
    {
        CloseConfirm();
    }

    private void CloseConfirm()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }
}