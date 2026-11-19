using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Transient chart selection when entering the single Mouse scene.
/// No scene object, RectTransform or saved chart asset is changed.
/// </summary>
public static class MouseModeSongSelection
{
    public static MouseModeChartAsset CurrentChart { get; private set; }

    public static void Choose(MouseModeChartAsset chart)
    {
        CurrentChart = chart;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnNewPlaySession()
    {
        CurrentChart = null;
    }
}
