using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimingSliderValue : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Slider slider;

    [Header("표시")]
    [SerializeField] private TMP_Text valueNumber;
    [SerializeField] private TMP_Text unitText;

    [Header("설정")]
    [SerializeField] private float minValue = -30f;
    [SerializeField] private float maxValue = 30f;

    private void Start()
    {
        if (slider == null || valueNumber == null)
        {
            Debug.LogWarning(
                "TimingSliderValue: Slider 또는 ValueNumber가 연결되지 않았습니다."
            );

            return;
        }

        slider.minValue = minValue;
        slider.maxValue = maxValue;

        if (unitText != null)
        {
            unitText.text = "ms";
        }

        float savedValue = 0f;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            savedValue =
                SaveManager.Instance.Data.judgeOffset;
        }

        savedValue =
            Mathf.Clamp(
                savedValue,
                minValue,
                maxValue
            );

        slider.SetValueWithoutNotify(
            savedValue
        );

        UpdateValueText(
            savedValue
        );

        slider.onValueChanged.AddListener(
            OnSliderValueChanged
        );
    }

    private void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(
                OnSliderValueChanged
            );
        }
    }

    private void OnSliderValueChanged(float value)
    {
        value =
            Mathf.Clamp(
                value,
                minValue,
                maxValue
            );

        UpdateValueText(
            value
        );

        int saveValue =
            Mathf.RoundToInt(value);

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.judgeOffset =
                saveValue;

            SaveManager.Instance.SaveNow();
        }

        RhythmManager rhythmManager =
            FindObjectOfType<RhythmManager>();

        if (rhythmManager != null)
        {
            rhythmManager.SetJudgeOffsetMs(
                saveValue
            );
        }
    }

    private void UpdateValueText(float value)
    {
        valueNumber.text =
            FormatValue(value);
    }

    private string FormatValue(float value)
    {
        if (Mathf.Approximately(
            value,
            Mathf.Round(value)
        ))
        {
            return Mathf
                .RoundToInt(value)
                .ToString();
        }

        return value.ToString("F1");
    }
}