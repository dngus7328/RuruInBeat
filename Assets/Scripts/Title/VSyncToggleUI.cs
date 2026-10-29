using UnityEngine;
using UnityEngine.UI;

public class VSyncToggleUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button onButton;
    [SerializeField] private Button offButton;

    [Header("버튼 이미지")]
    [SerializeField] private Image onButtonImage;
    [SerializeField] private Image offButtonImage;

    [Header("스프라이트")]
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite unselectedSprite;

    private bool currentValue = true;

    private void Start()
    {
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            currentValue =
                SaveManager.Instance.Data.vSync;
        }

        if (onButton != null)
        {
            onButton.onClick.AddListener(SetOn);
        }

        if (offButton != null)
        {
            offButton.onClick.AddListener(SetOff);
        }

        RefreshVisual();
        ApplyVSync();
    }

    private void OnDestroy()
    {
        if (onButton != null)
        {
            onButton.onClick.RemoveListener(SetOn);
        }

        if (offButton != null)
        {
            offButton.onClick.RemoveListener(SetOff);
        }
    }

    private void SetOn()
    {
        SetValue(true);
    }

    private void SetOff()
    {
        SetValue(false);
    }

    private void SetValue(bool value)
    {
        currentValue = value;

        RefreshVisual();

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SaveManager.Instance.Data.vSync =
                currentValue;

            SaveManager.Instance.SaveNow();
        }

        ApplyVSync();
    }

    private void ApplyVSync()
    {
        QualitySettings.vSyncCount =
            currentValue ? 1 : 0;

        Debug.Log(
            "VSync: " +
            (currentValue ? "ON" : "OFF") +
            " | vSyncCount: " +
            QualitySettings.vSyncCount
        );
    }

    private void RefreshVisual()
    {
        if (onButtonImage != null)
        {
            onButtonImage.sprite =
                currentValue
                    ? selectedSprite
                    : unselectedSprite;
        }

        if (offButtonImage != null)
        {
            offButtonImage.sprite =
                currentValue
                    ? unselectedSprite
                    : selectedSprite;
        }
    }
}