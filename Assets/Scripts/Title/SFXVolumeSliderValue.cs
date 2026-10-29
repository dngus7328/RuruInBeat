using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SFXVolumeSliderValue : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueNumber;

    private void Start()
    {
        if (slider == null || valueNumber == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;

        float value = 100f;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            value = SaveManager.Instance.Data.sfxVolume * 100f;
        }

        slider.SetValueWithoutNotify(value);
        UpdateText(value);

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
        value = Mathf.Clamp(value, 0f, 100f);

        UpdateText(value);

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.sfxVolume = value / 100f;
            SaveManager.Instance.SaveNow();
        }

        if (AudioSettingsManager.Instance != null)
        {
            AudioSettingsManager.Instance.ApplySFX();
        }
    }

    private void UpdateText(float value)
    {
        valueNumber.text =
            Mathf.RoundToInt(value).ToString();
    }
}
