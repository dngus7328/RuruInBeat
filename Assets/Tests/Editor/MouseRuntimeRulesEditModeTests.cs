using System;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// Pure runtime rules that must continue working without any PNG or prefab.
/// All tested methods are invoked from the game logic, not copied formulas.
/// </summary>
public sealed class MouseRuntimeRulesEditModeTests
{
    private static Type Resolve(string fullName)
    {
        Type type = Type.GetType(fullName + ", Assembly-CSharp");
        Assert.IsNotNull(type, "Missing runtime type " + fullName);
        return type;
    }

    private static object Call(Type type, string name, params object[] args)
    {
        MethodInfo method = type.GetMethod(name,
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "Missing method " + name);
        return method.Invoke(null, args);
    }

    [TestCase("PERFECT", true, 1)]
    [TestCase("GREAT", true, 0)]
    [TestCase("GOOD", true, -1)]
    [TestCase("MISS", true, -4)]
    [TestCase("MISS", false, 0)]
    public void RealRhythmNotesAdjustStabilityButEmptyClicksDoNot(
        string judgement, bool realNoteMiss, int expected)
    {
        Type director = Resolve("MouseModeBulletDirector");
        int value = (int)Call(director, "JudgementStabilityDelta",
            judgement, realNoteMiss, 1, 0, 1, 4);
        Assert.AreEqual(expected, value);
    }

    [TestCase(-200, -0.15d)]
    [TestCase(-150, -0.15d)]
    [TestCase(-30, -0.03d)]
    [TestCase(0, 0d)]
    [TestCase(15, 0.015d)]
    [TestCase(30, 0.03d)]
    [TestCase(150, 0.15d)]
    [TestCase(200, 0.15d)]
    public void SavedTimingOffsetUsesMillisecondsAndClamps(
        int offset, double expected)
    {
        Type player = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        double actual = (double)Call(player,
            "JudgeOffsetMillisecondsToSeconds", offset);
        Assert.AreEqual(expected, actual, 0.0000001d);
    }

    [TestCase(1d, 1.4d, .65d, .75d)]
    [TestCase(0d, 2d, .65d, 0d)]
    [TestCase(1.8d, 2d, .55d, 1.45d)]
    public void BulletTelegraphNeverStartsLaterThanMinimumWarningWindow(
        double proposed, double dangerousTime, double minimumLead, double expected)
    {
        Type director = Resolve("MouseModeBulletDirector");
        double actual = (double)Call(director, "EnsureTelegraphLead",
            proposed, dangerousTime, minimumLead);
        Assert.AreEqual(expected, actual, .0000001d);
    }

    [Test]
    public void PerSongRecordFieldsSurviveUnityJsonSerialization()
    {
        Type saveData = Resolve("SaveManager+SaveData");
        object source = Activator.CreateInstance(saveData);
        string json = UnityEngine.JsonUtility.ToJson(source);
        Assert.IsTrue(json.Contains("songBestRecords"),
            "Song bests must be serialized, not stored in a transient dictionary.");
        object reloaded = UnityEngine.JsonUtility.FromJson(json, saveData);
        Assert.IsNotNull(saveData.GetField("songBestRecords").GetValue(reloaded));
    }

    [Test]
    public void WrongButtonMashHasExplicitMissHandler()
    {
        Type player = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        Assert.IsNotNull(player.GetMethod("TryConsumeWrongButton",
            BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Test]
    public void RealNotesHaveDedicatedEventSeparateFromEmptyClickFeedback()
    {
        Type player = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        Assert.IsNotNull(player.GetEvent("ChartNoteJudged",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.IsNotNull(player.GetEvent("Judged",
            BindingFlags.Public | BindingFlags.Instance));
    }
}
