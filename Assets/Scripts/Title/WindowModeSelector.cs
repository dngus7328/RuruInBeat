using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WindowModeSelector : MonoBehaviour
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

    private readonly string[] values =
    {
        "전체 화면",
        "테두리 없는 창 모드",
        "창 모드"
    };

    private int currentIndex =
        SaveManager.WINDOW_MODE_BORDERLESS;

    private void Start()
    {
        if (
            SaveManager.Instance != null &&
            SaveManager.Instance.Data != null
        )
        {
            currentIndex =
                Mathf.Clamp(
                    SaveManager.Instance.Data.windowMode,
                    0,
                    values.Length - 1
                );
        }

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
        ApplyWindowMode();
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

    private void SelectPrevious()
    {
        if (currentIndex <= 0)
            return;

        currentIndex--;

        SaveAndApply();
    }

    private void SelectNext()
    {
        if (
            currentIndex >=
            values.Length - 1
        )
        {
            return;
        }

        currentIndex++;

        SaveAndApply();
    }

    private void SaveAndApply()
    {
        if (
            SaveManager.Instance != null &&
            SaveManager.Instance.Data != null
        )
        {
            SaveManager.Instance.Data.windowMode =
                currentIndex;

            SaveManager.Instance.Data.fullscreen =
                currentIndex !=
                SaveManager.WINDOW_MODE_WINDOWED;

            SaveManager.Instance.SaveNow();
        }

        Refresh();
        ApplyWindowMode();
    }

    private void ApplyWindowMode()
    {
        switch (currentIndex)
        {
            case SaveManager.WINDOW_MODE_FULLSCREEN:

                Screen.fullScreenMode =
                    FullScreenMode.ExclusiveFullScreen;

                break;

            case SaveManager.WINDOW_MODE_BORDERLESS:

                Screen.fullScreenMode =
                    FullScreenMode.FullScreenWindow;

                break;

            case SaveManager.WINDOW_MODE_WINDOWED:

                Screen.fullScreenMode =
                    FullScreenMode.Windowed;

                break;
        }
    }

    private void Refresh()
    {
        if (valueText != null)
        {
            valueText.text =
                values[currentIndex];
        }

        bool canGoLeft =
            currentIndex > 0;

        bool canGoRight =
            currentIndex <
            values.Length - 1;

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

    public void ResetToDefault()
    {
        currentIndex =
            SaveManager.WINDOW_MODE_BORDERLESS;

        if (
            SaveManager.Instance != null &&
            SaveManager.Instance.Data != null
        )
        {
            SaveManager.Instance.Data.windowMode =
                SaveManager.WINDOW_MODE_BORDERLESS;

            SaveManager.Instance.Data.fullscreen =
                true;
        }

        Refresh();
        ApplyWindowMode();
    }
}