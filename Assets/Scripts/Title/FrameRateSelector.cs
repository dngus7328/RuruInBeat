using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FrameRateSelector : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [SerializeField] private Image leftArrowImage;
    [SerializeField] private Image rightArrowImage;

    [SerializeField] private TMP_Text valueText;

    [Header("화살표 색")]
    [SerializeField]
    private Color activeArrowColor =
        Color.white;

    [SerializeField]
    private Color disabledArrowColor =
        new Color(
            0.35f,
            0.35f,
            0.35f,
            1f
        );

    private readonly int[] frameRates =
    {
        60,
        120,
        144,
        240
    };

    private int currentIndex = 1;

    private void Start()
    {
        currentIndex = FindSavedIndex();

        if (leftButton != null)
        {
            leftButton.onClick.AddListener(
                SelectPrevious
            );
        }

        if (rightButton != null)
        {
            rightButton.onClick.AddListener(
                SelectNext
            );
        }

        Refresh();
        ApplyFrameRate();
    }

    private void OnDestroy()
    {
        if (leftButton != null)
        {
            leftButton.onClick.RemoveListener(
                SelectPrevious
            );
        }

        if (rightButton != null)
        {
            rightButton.onClick.RemoveListener(
                SelectNext
            );
        }
    }

    private int FindSavedIndex()
    {
        int savedFrameRate = 120;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            savedFrameRate =
                SaveManager.Instance.Data.targetFrameRate;
        }

        for (int i = 0;
             i < frameRates.Length;
             i++)
        {
            if (frameRates[i] == savedFrameRate)
            {
                return i;
            }
        }

        return 1;
    }

    private void SelectPrevious()
    {
        if (currentIndex <= 0)
            return;

        currentIndex--;

        SaveAndApply();
    }

    private void SelectNext()
    {
        if (currentIndex >=
            frameRates.Length - 1)
        {
            return;
        }

        currentIndex++;

        SaveAndApply();
    }

    private void SaveAndApply()
    {
        Refresh();

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.targetFrameRate =
                frameRates[currentIndex];

            SaveManager.Instance.SaveNow();
        }

        ApplyFrameRate();
    }

    private void ApplyFrameRate()
    {
        Application.targetFrameRate =
            frameRates[currentIndex];

        Debug.Log(
            "최대 FPS: " +
            frameRates[currentIndex]
        );
    }

    private void Refresh()
    {
        if (valueText != null)
        {
            valueText.text =
                frameRates[currentIndex]
                    .ToString();
        }

        bool canGoLeft =
            currentIndex > 0;

        bool canGoRight =
            currentIndex <
            frameRates.Length - 1;

        if (leftButton != null)
        {
            leftButton.interactable =
                canGoLeft;
        }

        if (rightButton != null)
        {
            rightButton.interactable =
                canGoRight;
        }

        if (leftArrowImage != null)
        {
            leftArrowImage.color =
                canGoLeft
                    ? activeArrowColor
                    : disabledArrowColor;
        }

        if (rightArrowImage != null)
        {
            rightArrowImage.color =
                canGoRight
                    ? activeArrowColor
                    : disabledArrowColor;
        }
    }
}