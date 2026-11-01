using UnityEngine;
using UnityEngine.UI;

public class CreditsPanelController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Panel")]
    [SerializeField] private GameObject creditsPanel;

    private void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(
                OpenCredits
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                CloseCredits
            );
        }

        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(
                OpenCredits
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                CloseCredits
            );
        }
    }

    private void OpenCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    private void CloseCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }
}