using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class MouseModeResultFlowEditModeTests
{
    // EditMode tests live in their own asmdef. The game script is in the
    // predefined Assembly-CSharp, so resolve it by reflection instead of
    // adding an impossible asmdef -> predefined assembly reference.
    private static string CallResultMethod(string methodName, object argument)
    {
        Type type = Type.GetType("MouseModeResultFlow, Assembly-CSharp");
        Assert.IsNotNull(type, "The result flow must compile into Assembly-CSharp.");

        MethodInfo method = type.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "Missing public result method: " + methodName);

        return method.Invoke(null, new[] { argument }) as string;
    }

    [TestCase(1000000, "SS")]
    [TestCase(999999, "S")]
    [TestCase(900000, "S")]
    [TestCase(899999, "A")]
    [TestCase(800000, "A")]
    [TestCase(799999, "B")]
    [TestCase(700000, "B")]
    [TestCase(699999, "C")]
    [TestCase(600000, "C")]
    [TestCase(599999, "FAILED")]
    [TestCase(0, "FAILED")]
    [TestCase(-5, "FAILED")]
    [TestCase(1500000, "SS")]
    public void ScoreRanksFollowGameThresholds(int score, string expected)
    {
        Assert.AreEqual(expected, CallResultMethod("RankForScore", score));
    }

    [TestCase(0f, "00:00")]
    [TestCase(-2f, "00:00")]
    [TestCase(59.99f, "00:59")]
    [TestCase(125.9f, "02:05")]
    [TestCase(600f, "10:00")]
    public void PlayDurationIsDisplayedAsMinutesAndSeconds(
        float seconds, string expected)
    {
        Assert.AreEqual(expected, CallResultMethod("FormatTime", seconds));
    }
    [Test]
    public void ConsoleArtUsesSeparateNineSliceSprites()
    {
        Type type=Type.GetType("RuruConsoleUI, Assembly-CSharp");
        Assert.IsNotNull(type);
        Sprite previous=null;
        foreach(string name in new[]{"Shell","Inset","MainKey","AltKey"})
        {
            Sprite sprite=(Sprite)type.GetProperty(name).GetValue(null);
            Assert.IsNotNull(sprite,name);
            Assert.AreEqual(new Vector4(16,16,16,16),sprite.border);
            Assert.AreEqual(new Rect(0,0,64,64),sprite.rect);
            Assert.AreNotSame(previous,sprite);
            previous=sprite;
        }
    }

    [Test]
    public void ResultConsoleSeparatesLiveStatisticsAndActions()
    {
        var host=new GameObject("ResultTest",typeof(RectTransform),typeof(Canvas));
        host.SetActive(false);
        try
        {
            Type type=Type.GetType("MouseModeResultFlow, Assembly-CSharp");
            var flow=host.AddComponent(type);
            type.GetMethod("BuildFallbackPanel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
            var panel=host.transform.Find("ResultPanel_Runtime");
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.gameObject.activeSelf);
            var stage=panel.Find("ResultConsoleStage");
            Assert.IsNotNull(stage.Find("PinoMonitor/PortraitWell"));
            var stats=stage.Find("ResultStatisticsCard");
            foreach(string name in new[]{"PERFECT","GREAT","GOOD","MISS"})
                Assert.IsNotNull(stats.Find("Judgment_"+name+"/Value"));
            Assert.AreEqual(2,stage.GetComponentsInChildren<Button>(true).Length);
            foreach(RectTransform rt in stats.GetComponentsInChildren<RectTransform>(true))
            {
                Assert.LessOrEqual(rt.anchorMin.x,rt.anchorMax.x,rt.name);
                Assert.LessOrEqual(rt.anchorMin.y,rt.anchorMax.y,rt.name);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
