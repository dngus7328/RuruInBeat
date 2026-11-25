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
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool("Ruru.SimpleChart.Test.Active",false))
        {
            string path=UnityEditor.SessionState.GetString("Ruru.SimpleChart.Test.Asset","");
            if(!string.IsNullOrEmpty(path))CurrentChart=UnityEditor.AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
        }
#endif
    }
}
