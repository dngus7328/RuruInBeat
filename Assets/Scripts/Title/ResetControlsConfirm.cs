using UnityEngine;

public class ResetControlsConfirm : MonoBehaviour
{
    [SerializeField]
    private GameObject confirmPanel;

    public void Open()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    public void Confirm()
    {
        if (InputBindingManager.Instance != null)
        {
            InputBindingManager.Instance.ResetAllBindings();
        }

        Close();
    }

    public void Cancel()
    {
        Close();
    }

    private void Close()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }
}