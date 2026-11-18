using System.Reflection;
using LuluInBeat.MouseMode.ChartSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this component to GameplayCanvas/PlayArea/Background.
/// It displays the Sprite stored in the chart asset currently selected by
/// MouseModeChartPlayer. The background Image's size and placement are not changed.
/// It does not alter the chart player, music timing, note logic, or bullets.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MouseModeSongBackgroundUI : MonoBehaviour
{
    [Header("Song Source (auto-find if empty)")]
    [SerializeField] private MouseModeChartPlayer chartPlayer;

    [Header("PlayArea/Background Image (auto-find if empty)")]
    [SerializeField] private Image backgroundImage;

    private static readonly FieldInfo ChartAssetField =
        typeof(MouseModeChartPlayer).GetField("chartAsset",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private Sprite originalBackground;
    private MouseModeChartAsset lastSong;
    private Sprite lastSelectedSprite;
    private bool hasApplied;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage != null)
            originalBackground = backgroundImage.sprite;

        FindChartPlayerIfNeeded();
        RefreshBackground();
    }

    private void OnEnable()
    {
        RefreshBackground();
    }

    private void Update()
    {
        // The editor may switch the player's chart asset at runtime.
        // Only the Image.sprite is updated when the selected asset changes.
        RefreshBackground();
    }

    public void RefreshBackground()
    {
        if (backgroundImage == null || ChartAssetField == null)
            return;

        FindChartPlayerIfNeeded();
        if (chartPlayer == null)
            return;

        MouseModeChartAsset selectedSong =
            ChartAssetField.GetValue(chartPlayer) as MouseModeChartAsset;

        // Do not erase the existing background while a chart is not selected.
        if (selectedSong == null)
            return;

        Sprite selectedSprite = selectedSong.backgroundImage;
        if (hasApplied && selectedSong == lastSong &&
            selectedSprite == lastSelectedSprite)
            return;

        // Songs without an assigned background use the scene's original image.
        backgroundImage.sprite = selectedSprite != null
            ? selectedSprite
            : originalBackground;

        lastSong = selectedSong;
        lastSelectedSprite = selectedSprite;
        hasApplied = true;
    }

    private void FindChartPlayerIfNeeded()
    {
        if (chartPlayer != null)
            return;

        MouseModeChartPlayer[] players =
            FindObjectsOfType<MouseModeChartPlayer>(true);

        foreach (MouseModeChartPlayer candidate in players)
        {
            if (candidate != null && candidate.gameObject.scene == gameObject.scene)
            {
                chartPlayer = candidate;
                return;
            }
        }
    }
}
