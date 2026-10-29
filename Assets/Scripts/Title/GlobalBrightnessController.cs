using UnityEngine;
using UnityEngine.UI;

public class GlobalBrightnessController : MonoBehaviour
{
    public static GlobalBrightnessController Instance { get; private set; }

    private Canvas brightnessCanvas;
    private Image brightnessOverlay;

    private float currentBrightness = 100f;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void CreateAutomatically()
    {
        if (Instance != null)
            return;

        GlobalBrightnessController existing =
            FindFirstObjectByType<
                GlobalBrightnessController
            >();

        if (existing != null)
        {
            Instance = existing;
            existing.ApplySavedBrightness();
            return;
        }

        GameObject obj =
            new GameObject(
                "GlobalBrightnessController"
            );

        obj.AddComponent<
            GlobalBrightnessController
        >();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        CreateOverlay();
        ApplySavedBrightness();
    }

    private void CreateOverlay()
    {
        if (brightnessCanvas != null)
            return;

        GameObject canvasObject =
            new GameObject(
                "BrightnessCanvas"
            );

        canvasObject.transform.SetParent(
            transform,
            false
        );

        brightnessCanvas =
            canvasObject.AddComponent<Canvas>();

        brightnessCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        brightnessCanvas.sortingOrder =
            short.MaxValue;

        CanvasScaler scaler =
            canvasObject.AddComponent<
                CanvasScaler
            >();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GraphicRaycaster raycaster =
            canvasObject.AddComponent<
                GraphicRaycaster
            >();

        raycaster.enabled = false;

        GameObject overlayObject =
            new GameObject(
                "BrightnessOverlay"
            );

        overlayObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        brightnessOverlay =
            overlayObject.AddComponent<Image>();

        brightnessOverlay.raycastTarget =
            false;

        RectTransform rect =
            brightnessOverlay.rectTransform;

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        Color color =
            Color.black;

        color.a = 0f;

        brightnessOverlay.color =
            color;
    }

    public void ApplySavedBrightness()
    {
        float value = 100f;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            value =
                SaveManager.Instance.Data.brightness;
        }

        SetBrightness(value);
    }

    public void SetBrightness(float value)
    {
        currentBrightness =
            Mathf.Clamp(
                value,
                0f,
                100f
            );

        if (brightnessOverlay == null)
        {
            CreateOverlay();
        }

        if (brightnessOverlay == null)
            return;

        // 100% = 원래 화면
        // 0% = 완전히 검은 화면
        float alpha =
            1f -
            (currentBrightness / 100f);

        Color color =
            brightnessOverlay.color;

        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = alpha;

        brightnessOverlay.color =
            color;
    }

    public float GetBrightness()
    {
        return currentBrightness;
    }
}
