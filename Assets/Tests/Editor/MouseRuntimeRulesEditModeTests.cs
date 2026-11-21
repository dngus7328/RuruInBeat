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

    [Test]
    public void ProjectilesFollowTempoMapChangesInsteadOfFrozenOpeningBpm()
    {
        Type chartType = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartData");
        Type markerType = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeTempoChange");
        object chart = Activator.CreateInstance(chartType);
        chartType.GetField("bpm").SetValue(chart, 120f);
        chartType.GetField("beatZeroSeconds").SetValue(chart, 0f);

        object marker = Activator.CreateInstance(markerType);
        markerType.GetField("beat").SetValue(marker, 4f);
        markerType.GetField("bpm").SetValue(marker, 240f);
        System.Collections.IList changes = (System.Collections.IList)
            chartType.GetField("tempoChanges").GetValue(chart);
        changes.Add(marker);

        // Beat 3 at 1.5s, beat 4 at 2.0s, beat 6 at 2.5s.
        // A frozen 120 BPM would advance only two beats in one second;
        // the true tempo map advances three.
        Type director = Resolve("MouseModeBulletDirector");
        float elapsed = (float)Call(director, "ProjectileBeatAtTime",
            chart, 3f, 2.5d, 1.5d, .5f);
        Assert.AreEqual(3f, elapsed, .0001f);

        float oldChartFallback = (float)Call(director,
            "ProjectileBeatAtTime", null, 3f, 2.5d, 1.5d, .5f);
        Assert.AreEqual(2f, oldChartFallback, .0001f);
    }

    [TestCase(0, false, 0)]
    [TestCase(1, false, 1)]
    [TestCase(2, false, 2)]
    [TestCase(3, false, 3)]
    [TestCase(0, true, 2)]
    [TestCase(1, true, 2)]
    public void BulletVisualsStayDistinctByAttackRole(
        int colorIndex, bool fragment, int expected)
    {
        Type director = Resolve("MouseModeBulletDirector");
        int key = (int)Call(director, "ProjectileSilhouetteKey",
            colorIndex, fragment);
        Assert.AreEqual(expected, key);
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

    [TestCase(-1.5d, 0.14d, 2.2d, false)]
    [TestCase(-0.01d, 0.14d, 2.2d, false)]
    [TestCase(0d, 0.14d, 2.2d, true)]
    [TestCase(0.14d, 0.14d, 2.2d, true)]
    [TestCase(2d, 3d, 0.70d, false)]
    [TestCase(2.30d, 3d, 0.70d, true)]
    public void NoNotesAppearUntilScheduledAudioStart(
        double songSeconds, double hitTime, double lead, bool expected)
    {
        Type player = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        bool actual = (bool)Call(player, "CanSpawnNoteAfterSongBegins",
            songSeconds, hitTime, lead);
        Assert.AreEqual(expected, actual);
    }

    [TestCase(0.14283d, 0.06283d)]
    [TestCase(0.19d, 0.11d)]
    [TestCase(0.35d, 0.27d)]
    [TestCase(0.46d, 0.38d)]
    [TestCase(0.69666d, 0.5d)]
    [TestCase(1.81631d, 0.5d)]
    [TestCase(0.05d, 0d)]
    [TestCase(double.PositiveInfinity, 0.5d)]
    public void GoOverlayClearsBeforeOpeningNotesWithoutShiftingMusic(
        double firstHit, double expected)
    {
        Type player = Resolve(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        double duration = (double)Call(player, "GoVisibleSeconds", firstHit);
        Assert.AreEqual(expected, duration, 0.000001d);
        Assert.IsNotNull(player.GetProperty("HasReachedSongStart",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [TestCase(11f, 12f, 15f, false)]
    [TestCase(12f, 12f, 15f, false)]
    [TestCase(13f, 12f, 15f, false)]
    [TestCase(16f, 12f, 15f, true)]
    [TestCase(27f, 12f, 15f, true)]
    [TestCase(30f, 12f, 15f, false)]
    public void GrazeRewardsRequireSafeClosePasses(
        float distance, float radius, float bonus, bool expected)
    {
        Type director = Resolve("MouseModeBulletDirector");
        bool actual = (bool)Call(director, "InGrazeBand",
            distance, radius, bonus);
        Assert.AreEqual(expected, actual);
    }

    [TestCase(900000, 0, 0, 900000)]
    [TestCase(900000, 0, 5, 905000)]
    [TestCase(900000, 1, 5, 895000)]
    [TestCase(950000, 0, 40, 975000)]
    [TestCase(999999, 0, 40, 999999)]
    [TestCase(1000000, 0, 40, 1000000)]
    [TestCase(1000000, 1, 40, 999999)]
    public void SafeDodgesRewardScoreButCannotForgeAnSSRank(
        int baseScore, int hits, int grazes, int expected)
    {
        Type director = Resolve("MouseModeBulletDirector");
        int actual = (int)Call(director, "CalculateBattleScore",
            baseScore, hits, grazes);
        Assert.AreEqual(expected, actual);
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
