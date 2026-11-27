using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RuruStoryEditModeTests
{
    private static Type GameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name);
        return type;
    }

    [Test]
    public void StorySceneAndEditableTutorialAssetsAreRegistered()
    {
        Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled &&
            s.path == "Assets/Scene/StoryScene.unity"));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/TutorialStory.asset");
        Assert.IsNotNull(asset);
        var data = new SerializedObject(asset);
        Assert.GreaterOrEqual(data.FindProperty("opening").arraySize, 0);
        Assert.GreaterOrEqual(data.FindProperty("closing").arraySize, 0);
        Assert.IsNotNull(data.FindProperty("chapterNames"));
        Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath("Assets/Resources/StoryFlowSettings.asset"));
        Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath("Assets/Scene/StoryScene.unity"));
    }

    [Test]
    public void RouteSelectionPreservesOrderAndSkipsCompletedOrEmptyStories()
    {
        var settings = ScriptableObject.CreateInstance(GameType("StoryFlowSettings"));
        var first = ScriptableObject.CreateInstance(GameType("StoryData"));
        var second = ScriptableObject.CreateInstance(GameType("StoryData"));
        var empty = ScriptableObject.CreateInstance(GameType("StoryData"));
        Type storyType = first.GetType(), routeType = GameType("StoryRoute"), lineType = GameType("StoryLine");
        try
        {
            storyType.GetField("storyId").SetValue(first, "first");
            storyType.GetField("storyId").SetValue(second, "second");
            storyType.GetField("storyId").SetValue(empty, "empty");
            Array lines = Array.CreateInstance(lineType, 1);
            lines.SetValue(Activator.CreateInstance(lineType), 0);
            storyType.GetField("lines").SetValue(first, lines);
            storyType.GetField("lines").SetValue(second, lines);
            Array routes = Array.CreateInstance(routeType, 4);
            Type triggerType = GameType("StoryTrigger");
            var trigger = Enum.Parse(triggerType, "BeforeTutorial");
            var stories = new[] { first, empty, second, first };
            for (int i = 0; i < stories.Length; i++)
            {
                var route = Activator.CreateInstance(routeType);
                routeType.GetField("story").SetValue(route, stories[i]);
                routeType.GetField("trigger").SetValue(route, trigger);
                routeType.GetField("oncePerSave").SetValue(route, true);
                routes.SetValue(route, i);
            }
            settings.GetType().GetField("routes").SetValue(settings, routes);
            var select = GameType("StoryFlow").GetMethod("SelectRoutes");
            var chosen = (Array)select.Invoke(null, new object[] { settings, trigger, null, new List<string>() });
            Assert.AreEqual(2, chosen.Length);
            Assert.AreSame(first, routeType.GetField("story").GetValue(chosen.GetValue(0)));
            Assert.AreSame(second, routeType.GetField("story").GetValue(chosen.GetValue(1)));
            chosen = (Array)select.Invoke(null, new object[] { settings, trigger, null, new List<string> { "first" } });
            Assert.AreEqual(1, chosen.Length);
            Assert.AreSame(second, routeType.GetField("story").GetValue(chosen.GetValue(0)));
            var other = Enum.Parse(triggerType, "AfterTutorial");
            chosen = (Array)select.Invoke(null, new object[] { settings, other, null, null });
            Assert.AreEqual(0, chosen.Length);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(empty);
        }
    }
}
