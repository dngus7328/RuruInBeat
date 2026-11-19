using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Auto-maintained list of actual editor-created chart assets.
/// See Assets/Editor/SongSelectChartCatalogSync.cs.
/// </summary>
public sealed class SongSelectChartCatalog : ScriptableObject
{
    [Tooltip("Populated from existing chart .asset files in the editor.")]
    public MouseModeChartAsset[] chartFiles = new MouseModeChartAsset[0];
}
