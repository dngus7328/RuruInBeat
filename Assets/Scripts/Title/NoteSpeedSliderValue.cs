using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NoteSpeedSliderValue : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueNumber;

    [Header("설정")]
    [SerializeField] private float minValue = 1f;
    [SerializeField] private float maxValue = 10f;
    [SerializeField] private float defaultValue = 5f;
    [SerializeField] private float step = 0.1f;

    private void Start()
    {
        if (slider == null || valueNumber == null)
            return;

        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;

        float value = defaultValue;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null &&
            SaveManager.Instance.Data.noteSpeed > 0f)
        {
            value = SaveManager.Instance.Data.noteSpeed;
        }

        value = NormalizeValue(value);

        slider.SetValueWithoutNotify(value);
        UpdateValueText(value);

        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }
    }

    private void OnSliderChanged(float rawValue)
    {
        float value = NormalizeValue(rawValue);

        slider.SetValueWithoutNotify(value);
        UpdateValueText(value);

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.noteSpeed = value;
            SaveManager.Instance.SaveNow();
        }

        ChartPlayer chartPlayer =
            FindFirstObjectByType<ChartPlayer>();

        if (chartPlayer != null)
        {
            chartPlayer.SetNoteSpeed(value);
        }
    }

    private float NormalizeValue(float value)
    {
        value = Mathf.Clamp(
            value,
            minValue,
            maxValue
        );

        value =
            Mathf.Round(value / step) *
            step;

        return Mathf.Clamp(
            value,
            minValue,
            maxValue
        );
    }

    private void UpdateValueText(float value)
    {
        if (Mathf.Approximately(
            value,
            Mathf.Round(value)
        ))
        {
            valueNumber.text =
                Mathf.RoundToInt(value).ToString();
        }
        else
        {
            valueNumber.text =
                value.ToString("F1");
        }
    }
}