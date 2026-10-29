using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResolutionSelector : MonoBehaviour
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

    private readonly Vector2Int[] resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160)
    };

    private int currentIndex;

    private void Start()
    {
        currentIndex = 2;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            currentIndex =
                Mathf.Clamp(
                    SaveManager.Instance.Data.resolutionIndex,
                    0,
                    resolutions.Length - 1
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
        ApplyResolution();
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
        if (currentIndex >=
            resolutions.Length - 1)
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
            SaveManager.Instance.Data.resolutionIndex =
                currentIndex;

            SaveManager.Instance.SaveNow();
        }

        ApplyResolution();
    }

    private void ApplyResolution()
    {
        Vector2Int resolution =
            resolutions[currentIndex];

        Screen.SetResolution(
            resolution.x,
            resolution.y,
            Screen.fullScreenMode
        );
    }

    private void Refresh()
    {
        Vector2Int resolution =
            resolutions[currentIndex];

        if (valueText != null)
        {
            valueText.text =
                resolution.x +
                " × " +
                resolution.y;
        }

        bool canGoLeft =
            currentIndex > 0;

        bool canGoRight =
            currentIndex <
            resolutions.Length - 1;

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