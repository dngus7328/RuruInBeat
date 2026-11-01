using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResetAllSettingsButton : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Popup")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text messageText;

    [Header("Settings Root")]
    [SerializeField] private Transform settingsRoot;

    private void Awake()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(
                OpenConfirm
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(
                ConfirmReset
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(
                CancelReset
            );
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(
                OpenConfirm
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(
                ConfirmReset
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                CancelReset
            );
        }
    }

    // =========================================================
    // Popup
    // =========================================================

    private void OpenConfirm()
    {
        if (messageText != null)
        {
            messageText.text =
                "모든 설정을 기본값으로 초기화합니다. " +
                "하시겠습니까?";
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    private void CancelReset()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // =========================================================
    // Confirm Reset
    // =========================================================

    private void ConfirmReset()
    {
        if (
            SaveManager.Instance == null ||
            SaveManager.Instance.Data == null
        )
        {
            Debug.LogError(
                "ResetAllSettingsButton: SaveManager가 없습니다."
            );

            return;
        }

        // 저장값 기본값으로 변경
        ResetSaveData();

        // 게임 설정
        ResetGameSettings();

        // 사운드 설정
        ResetAudioSettings();

        // 화면 설정
        ResetDisplaySettings();

        // 키 설정
        ResetKeySettings();

        // 최종 저장
        SaveManager.Instance.SaveNow();

        // 확인창만 닫음
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        Canvas.ForceUpdateCanvases();
    }

    // =========================================================
    // Save Data Defaults
    // =========================================================

    private void ResetSaveData()
    {
        SaveManager.SaveData data =
            SaveManager.Instance.Data;

        // =========================
        // GAME
        // =========================

        data.judgeOffset = 0;
        data.noteSpeed = 5f;
        data.brightness = 100f;
        data.effectDisplay = true;

        // =========================
        // AUDIO
        // =========================

        data.masterVolume = 1f;
        data.musicVolume = 1f;
        data.sfxVolume = 1f;
        data.hitSoundVolume = 1f;

        // =========================
        // DISPLAY
        // =========================

        data.windowMode =
            SaveManager.WINDOW_MODE_BORDERLESS;

        data.resolutionIndex =
            SaveManager.RESOLUTION_NATIVE;

        data.fullscreen = true;

        data.vSync = false;

        data.targetFrameRate = 120;
    }

    // =========================================================
    // GAME
    // =========================================================

    private void ResetGameSettings()
    {
        if (settingsRoot == null)
        {
            Debug.LogError(
                "ResetAllSettingsButton: Settings Root가 연결되지 않았습니다."
            );

            return;
        }

        // -------------------------
        // Timing
        // -------------------------

        TimingSliderValue[] timingSliders =
            settingsRoot.GetComponentsInChildren<TimingSliderValue>(
                true
            );

        foreach (
            TimingSliderValue target
            in timingSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderValueChanged",
                0f
            );
        }

        // -------------------------
        // Note Speed
        // -------------------------

        NoteSpeedSliderValue[] noteSpeedSliders =
            settingsRoot.GetComponentsInChildren<NoteSpeedSliderValue>(
                true
            );

        foreach (
            NoteSpeedSliderValue target
            in noteSpeedSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                5f
            );
        }

        // -------------------------
        // Brightness
        // -------------------------

        BrightnessSliderValue[] brightnessSliders =
            settingsRoot.GetComponentsInChildren<BrightnessSliderValue>(
                true
            );

        foreach (
            BrightnessSliderValue target
            in brightnessSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                100f
            );
        }
    }

    // =========================================================
    // AUDIO
    // =========================================================

    private void ResetAudioSettings()
    {
        if (settingsRoot == null)
            return;

        // -------------------------
        // Master Volume
        // -------------------------

        MasterVolumeSliderValue[] masterSliders =
            settingsRoot.GetComponentsInChildren<MasterVolumeSliderValue>(
                true
            );

        foreach (
            MasterVolumeSliderValue target
            in masterSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                100f
            );
        }

        // -------------------------
        // Music Volume
        // -------------------------

        MusicVolumeSliderValue[] musicSliders =
            settingsRoot.GetComponentsInChildren<MusicVolumeSliderValue>(
                true
            );

        foreach (
            MusicVolumeSliderValue target
            in musicSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                100f
            );
        }

        // -------------------------
        // SFX Volume
        // -------------------------

        SFXVolumeSliderValue[] sfxSliders =
            settingsRoot.GetComponentsInChildren<SFXVolumeSliderValue>(
                true
            );

        foreach (
            SFXVolumeSliderValue target
            in sfxSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                100f
            );
        }

        // -------------------------
        // Hit Sound Volume
        // -------------------------

        HitSoundVolumeSliderValue[] hitSoundSliders =
            settingsRoot.GetComponentsInChildren<HitSoundVolumeSliderValue>(
                true
            );

        foreach (
            HitSoundVolumeSliderValue target
            in hitSoundSliders
        )
        {
            SetSliderAndInvoke(
                target,
                "OnSliderChanged",
                100f
            );
        }
    }

    // =========================================================
    // DISPLAY
    // =========================================================

    private void ResetDisplaySettings()
    {
        if (settingsRoot == null)
            return;

        // -------------------------
        // Window Mode
        // Borderless Window
        // -------------------------

        WindowModeSelector windowMode =
            settingsRoot.GetComponentInChildren<WindowModeSelector>(
                true
            );

        if (windowMode != null)
        {
            windowMode.ResetToDefault();
        }

        // -------------------------
        // Resolution
        // Monitor Native
        // -------------------------

        ResolutionSelector resolution =
            settingsRoot.GetComponentInChildren<ResolutionSelector>(
                true
            );

        if (resolution != null)
        {
            resolution.ResetToDefault();
        }

        // -------------------------
        // Frame Rate
        // 120 FPS
        // -------------------------

        FrameRateSelector frameRate =
            settingsRoot.GetComponentInChildren<FrameRateSelector>(
                true
            );

        if (frameRate != null)
        {
            frameRate.ResetToDefault();
        }

        // -------------------------
        // VSync
        // OFF
        // -------------------------

        VSyncToggleUI vSync =
            settingsRoot.GetComponentInChildren<VSyncToggleUI>(
                true
            );

        if (vSync != null)
        {
            vSync.ResetToDefault();
        }
        else
        {
            // UI가 없더라도 실제 값은 초기화
            SaveManager.Instance.Data.vSync =
                false;

            QualitySettings.vSyncCount =
                0;
        }
    }

    // =========================================================
    // KEY
    // =========================================================

    private void ResetKeySettings()
    {
        if (InputBindingManager.Instance == null)
            return;

        InputBindingManager.Instance
            .ResetAllBindings();
    }

    // =========================================================
    // Slider Reset Utility
    // =========================================================

    private void SetSliderAndInvoke(
        MonoBehaviour target,
        string methodName,
        float value
    )
    {
        if (target == null)
            return;

        System.Type targetType =
            target.GetType();

        // -------------------------
        // private Slider slider 찾기
        // -------------------------

        FieldInfo sliderField =
            targetType.GetField(
                "slider",
                BindingFlags.Instance |
                BindingFlags.NonPublic
            );

        if (sliderField == null)
        {
            Debug.LogWarning(
                targetType.Name +
                ": slider 필드를 찾지 못했습니다."
            );

            return;
        }

        Slider slider =
            sliderField.GetValue(target)
            as Slider;

        if (slider == null)
        {
            Debug.LogWarning(
                targetType.Name +
                ": Slider가 연결되어 있지 않습니다."
            );

            return;
        }

        // 실제 Slider 위치 변경
        slider.SetValueWithoutNotify(
            value
        );

        // -------------------------
        // 기존 설정 적용 함수 찾기
        // -------------------------

        MethodInfo method =
            targetType.GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic
            );

        if (method == null)
        {
            Debug.LogWarning(
                targetType.Name +
                ": " +
                methodName +
                " 함수를 찾지 못했습니다."
            );

            return;
        }

        // 기존 함수 실행
        // UI 숫자 + 저장 + 실제 적용
        method.Invoke(
            target,
            new object[]
            {
                value
            }
        );
    }
}