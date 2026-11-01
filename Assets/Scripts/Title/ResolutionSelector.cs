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

    // -1 = 권장 해상도
    private int currentIndex =
        SaveManager.RESOLUTION_NATIVE;

    private void Start()
    {
        if (
            SaveManager.Instance != null &&
            SaveManager.Instance.Data != null
        )
        {
            currentIndex =
                SaveManager.Instance.Data.resolutionIndex;
        }

        if (
            currentIndex <
            SaveManager.RESOLUTION_NATIVE ||
            currentIndex >= resolutions.Length
        )
        {
            currentIndex =
                SaveManager.RESOLUTION_NATIVE;
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
        if (
            currentIndex <=
            SaveManager.RESOLUTION_NATIVE
        )
        {
            return;
        }

        currentIndex--;

        SaveAndApply();
    }

    private void SelectNext()
    {
        if (
            currentIndex >=
            resolutions.Length - 1
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
            SaveManager.Instance.Data.resolutionIndex =
                currentIndex;

            SaveManager.Instance.SaveNow();
        }

        Refresh();
        ApplyResolution();
    }

    private void ApplyResolution()
    {
        int width;
        int height;

        if (
            currentIndex ==
            SaveManager.RESOLUTION_NATIVE
        )
        {
            width =
                Screen.currentResolution.width;

            height =
                Screen.currentResolution.height;
        }
        else
        {
            Vector2Int resolution =
                resolutions[currentIndex];

            width =
                resolution.x;

            height =
                resolution.y;
        }

        Screen.SetResolution(
            width,
            height,
            Screen.fullScreenMode
        );
    }

    private void Refresh()
    {
        if (valueText != null)
        {
            if (
                currentIndex ==
                SaveManager.RESOLUTION_NATIVE
            )
            {
                valueText.text =
                    "권장 해상도";
            }
            else
            {
                Vector2Int resolution =
                    resolutions[currentIndex];

                valueText.text =
                    resolution.x +
                    " × " +
                    resolution.y;
            }
        }

        bool canGoLeft =
            currentIndex >
            SaveManager.RESOLUTION_NATIVE;

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

    public void ResetToDefault()
    {
        currentIndex =
            SaveManager.RESOLUTION_NATIVE;

        if (
            SaveManager.Instance != null &&
            SaveManager.Instance.Data != null
        )
        {
            SaveManager.Instance.Data.resolutionIndex =
                SaveManager.RESOLUTION_NATIVE;
        }

        Refresh();
        ApplyResolution();
    }
}