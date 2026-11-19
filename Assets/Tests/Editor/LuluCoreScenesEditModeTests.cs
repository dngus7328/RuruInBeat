using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class LuluCoreScenesEditModeTests
{
    private static readonly string[] CoreScenes =
    {
        "Assets/Scene/TitleScene.unity",
        "Assets/Scene/SongSelectScene.unity",
        "Assets/Scene/Mouse.unity"
    };

    [Test]
    public void MainMenuAndSharedMouseScenesExistInBuild()
    {
        string[] enabled = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        foreach (string scene in CoreScenes)
        {
            Assert.Contains(scene, enabled,
                "A required scene is not enabled in Build Settings: " + scene);
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(scene),
                "The scene asset is missing: " + scene);
        }
    }

    [Test]
    public void LegacySongSpecificScenesStayDisabled()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path.StartsWith("Assets/Scene/Mouse_",
                StringComparison.OrdinalIgnoreCase))
                Assert.IsFalse(scene.enabled,
                    "Legacy song-specific scene should not be built: " +
                    scene.path);
        }
    }

    [Test]
    public void ChartCatalogReferencesRealChartAssets()
    {
        var catalog = AssetDatabase.LoadMainAssetAtPath(
            "Assets/Resources/SongSelectChartCatalog.asset");
        Assert.IsNotNull(catalog, "Song selection chart catalog is missing.");

        var serialized = new SerializedObject(catalog);
        SerializedProperty charts = serialized.FindProperty("chartFiles");
        Assert.IsNotNull(charts, "Chart catalog has no chartFiles array.");
        Assert.GreaterOrEqual(charts.arraySize, 6,
            "Expected at least six chart entries in the song catalog.");

        for (int i = 0; i < charts.arraySize; i++)
            Assert.IsNotNull(
                charts.GetArrayElementAtIndex(i).objectReferenceValue,
                "Chart catalog entry " + i + " refers to a missing asset.");
    }
}
