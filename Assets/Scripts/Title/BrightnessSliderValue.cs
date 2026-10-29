using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BrightnessSliderValue : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueNumber;
    [SerializeField] private TMP_Text unitText;

    [Header("설정")]
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float defaultValue = 100f;

    private void Start()
    {
        if (slider == null ||
            valueNumber == null)
        {
            Debug.LogWarning(
                "BrightnessSliderValue: Slider 또는 ValueNumber가 연결되지 않았습니다."
            );

            return;
        }

        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = true;

        if (unitText != null)
        {
            unitText.text = "%";
        }

        float value =
            defaultValue;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            value =
                SaveManager.Instance.Data.brightness;
        }

        value =
            Mathf.Clamp(
                value,
                minValue,
                maxValue
            );

        slider.SetValueWithoutNotify(
            value
        );

        UpdateValueText(value);
        ApplyBrightness(value);

        slider.onValueChanged.AddListener(
            OnSliderChanged
        );
    }

    private void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(
                OnSliderChanged
            );
        }
    }

    private void OnSliderChanged(float value)
    {
        value =
            Mathf.Clamp(
                value,
                minValue,
                maxValue
            );

        UpdateValueText(value);
        ApplyBrightness(value);

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.brightness =
                value;

            SaveManager.Instance.SaveNow();
        }
    }

    private void ApplyBrightness(float value)
    {
        if (GlobalBrightnessController.Instance != null)
        {
            GlobalBrightnessController.Instance.SetBrightness(
                value
            );
        }
    }

    private void UpdateValueText(float value)
    {
        if (Mathf.Approximately(
            value,
            Mathf.Round(value)
        ))
        {
            valueNumber.text =
                Mathf.RoundToInt(value)
                    .ToString();
        }
        else
        {
            valueNumber.text =
                value.ToString("F1");
        }
    }
}
