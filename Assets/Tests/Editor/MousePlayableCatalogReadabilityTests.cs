using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fails if any chart actually selected by SongSelect can obscure its own
/// solid hit targets, given both the reference and a compact PC layout.
/// The audit uses Mouse.unity's Inspector sizes and moving wheel paths.
/// No chart data is altered by these tests.
/// </summary>
public sealed class MousePlayableCatalogReadabilityTests
{
    private const string CatalogPath =
        "Assets/Resources/SongSelectChartCatalog.asset";
    private const string SummaryPath =
        "Reports/MouseChartAuditSummary.csv";

    private static void RunReadOnlyEditorAudit()
    {
        Type type = Type.GetType("MouseChartBatchAudit, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "Missing batch audit Editor script.");
        MethodInfo run = type.GetMethod("AuditAll",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(run);
        run.Invoke(null, null);
        Assert.IsTrue(File.Exists(SummaryPath),
            "Chart audit did not produce its summary CSV.");
    }

    [Test]
    public void EverySelectableMouseSongHasNoVisibleBodyOverlap()
    {
        RunReadOnlyEditorAudit();

        UnityEngine.Object catalog = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
        Assert.IsNotNull(catalog, "Song selection catalog is missing.");

        var serialized = new SerializedObject(catalog);
        SerializedProperty charts = serialized.FindProperty("chartFiles");
        Assert.IsNotNull(charts);

        var byClip = new Dictionary<AudioClip, List<UnityEngine.Object>>();
        for (int i = 0; i < charts.arraySize; i++)
        {
            UnityEngine.Object chart =
                charts.GetArrayElementAtIndex(i).objectReferenceValue;
            if (chart == null) continue;

            var data = new SerializedObject(chart);
            SerializedProperty hidden = data.FindProperty("hideFromSongSelect");
            SerializedProperty song = data.FindProperty("song");
            SerializedProperty info = data.FindProperty("data");
            if (hidden == null || hidden.boolValue || song == null ||
                info == null || song.objectReferenceValue == null)
                continue;

            AudioClip clip = song.objectReferenceValue as AudioClip;
            if (clip == null) continue;
            if (!byClip.ContainsKey(clip))
                byClip.Add(clip, new List<UnityEngine.Object>());
            byClip[clip].Add(chart);
        }

        Assert.GreaterOrEqual(byClip.Count, 5, "No complete song catalog.");

        string[] rows = File.ReadAllLines(SummaryPath).Skip(1).ToArray();
        foreach (KeyValuePair<AudioClip, List<UnityEngine.Object>> song in byClip)
        {
            // Same selection rule as SongSelectMenuUI.BuildCatalog:
            // one chart per AudioClip, prefer difficulty=MOUSE.
            UnityEngine.Object primary = song.Value.FirstOrDefault(chart => {
                SerializedProperty prop = new SerializedObject(chart)
                    .FindProperty("data.difficulty");
                return prop != null &&
                    string.Equals(prop.stringValue, "MOUSE",
                        StringComparison.OrdinalIgnoreCase);
            }) ?? song.Value[0];
            string assetPath = AssetDatabase.GetAssetPath(primary);

            string[] matching = rows.Where(line =>
                line.StartsWith("\"" + assetPath + "\",",
                    StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(2, matching.Length, "Missing both geometry scenarios: " +
                assetPath);

            foreach (string row in matching)
            {
                string[] cells = row.Split(',');
                Assert.GreaterOrEqual(cells.Length, 15,
                    "Incomplete chart audit CSV: " + row);
                Assert.AreEqual(0, int.Parse(cells[8]),
                    "Overlapping visible note bodies in selected chart " +
                    assetPath + " / " + cells[1]);
                Assert.AreEqual(0, int.Parse(cells[11]),
                    "Invalid serialized notes in selected chart " + assetPath);
            }
        }
    }
}
