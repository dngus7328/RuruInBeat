#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Only updates Assets/Resources/SongSelectChartCatalog.asset data.
/// Never opens, saves, rebuilds or moves a user's SongSelectScene UI.
/// </summary>
[InitializeOnLoad]
public static class SongSelectChartCatalogSync
{
    private const string CatalogFolder = "Assets/Resources";
    private const string CatalogPath =
        "Assets/Resources/SongSelectChartCatalog.asset";
    private const string ChartFolder =
        "Assets/MouseMode/ChartSystem/Charts";

    private static bool refreshing;
    private static bool queued;

    static SongSelectChartCatalogSync()
    {
        EditorApplication.delayCall += QueueRefresh;
        EditorApplication.projectChanged += QueueRefresh;
    }

    [MenuItem("Tools/Lulu In Beat/Song Select/Refresh Chart Files")]
    public static void QueueRefresh()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        queued = true;
        EditorApplication.delayCall += Refresh;
    }

    private static void Refresh()
    {
        queued = false;
        if (refreshing || EditorApplication.isPlayingOrWillChangePlaymode ||
            !AssetDatabase.IsValidFolder(ChartFolder))
            return;

        refreshing = true;
        try
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:MouseModeChartAsset", new[] { ChartFolder });
            var paths = new List<string>();
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            var charts = new List<MouseModeChartAsset>();
            foreach (string path in paths)
            {
                MouseModeChartAsset chart =
                    AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
                if (chart == null || chart.song == null ||
                    chart.data == null ||
                    chart.data.level < 1 || chart.data.level > 15 ||
                    chart.data.notes == null || chart.data.notes.Count == 0)
                    continue;

                charts.Add(chart);
            }

            SongSelectChartCatalog catalog =
                AssetDatabase.LoadAssetAtPath<SongSelectChartCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                if (!AssetDatabase.IsValidFolder(CatalogFolder))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                catalog = ScriptableObject.CreateInstance<SongSelectChartCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            MouseModeChartAsset[] old = catalog.chartFiles;
            bool unchanged = old != null && old.Length == charts.Count;
            if (unchanged)
                for (int i = 0; i < charts.Count; i++)
                    if (old[i] != charts[i]) { unchanged = false; break; }
            if (unchanged) return;

            catalog.chartFiles = charts.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[SongSelect] Chart catalog refreshed: " +
                charts.Count + " valid chart assets. UI layout untouched.");
        }
        finally
        {
            refreshing = false;
        }
    }
}

/// <summary>
/// Track chart creation, deletion or reimport without touching scene geometry.
/// Regular projectChanged also catches inspector/save edits.
/// </summary>
public sealed class SongSelectChartAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported,
        string[] deleted, string[] movedTo, string[] movedFrom)
    {
        if (ContainsChart(imported) || ContainsChart(deleted) ||
            ContainsChart(movedTo) || ContainsChart(movedFrom))
            SongSelectChartCatalogSync.QueueRefresh();
    }

    private static bool ContainsChart(string[] paths)
    {
        if (paths == null) return false;
        foreach (string path in paths)
            if (path.StartsWith(
                "Assets/MouseMode/ChartSystem/Charts/",
                StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
#endif
