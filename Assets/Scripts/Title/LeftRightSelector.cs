using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeftRightSelector : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [SerializeField] private Image leftArrowImage;
    [SerializeField] private Image rightArrowImage;

    [SerializeField] private TMP_Text valueText;

    [Header("선택 값")]
    [SerializeField] private string[] values;

    [Header("시작 인덱스")]
    [SerializeField] private int startIndex = 0;

    [Header("화살표 색")]
    [SerializeField] private Color activeArrowColor = Color.white;

    [SerializeField]
    private Color disabledArrowColor =
        new Color(
            0.35f,
            0.35f,
            0.35f,
            1f
        );

    private int currentIndex;

    private void Start()
    {
        if (values == null ||
            values.Length == 0)
        {
            Debug.LogWarning(
                name +
                ": LeftRightSelector에 값이 없습니다."
            );

            return;
        }

        currentIndex =
            Mathf.Clamp(
                startIndex,
                0,
                values.Length - 1
            );

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

        Refresh();
        OnValueChanged();
    }

    private void SelectNext()
    {
        if (currentIndex >=
            values.Length - 1)
        {
            return;
        }

        currentIndex++;

        Refresh();
        OnValueChanged();
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

    private void OnValueChanged()
    {
        Debug.Log(
            name +
            " 선택값: " +
            values[currentIndex]
        );
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public string GetCurrentValue()
    {
        if (values == null ||
            values.Length == 0)
        {
            return "";
        }

        return values[currentIndex];
    }

    public void SetIndex(
        int index
    )
    {
        if (values == null ||
            values.Length == 0)
        {
            return;
        }

        currentIndex =
            Mathf.Clamp(
                index,
                0,
                values.Length - 1
            );

        Refresh();
    }
}