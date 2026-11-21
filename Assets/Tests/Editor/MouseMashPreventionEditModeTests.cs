using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A correct-timed click must also hit the visible circular note body.
/// Repeated presses well before the judgement window cannot be farmed for points.
/// The actual MouseModeChartPlayer entry points call both tested predicates.
/// </summary>
public sealed class MouseMashPreventionEditModeTests
{
    private const string PlayerName =
        "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer, Assembly-CSharp";

    private static bool CircleContains(Vector2 point, Vector2 size)
    {
        Type player = Type.GetType(PlayerName);
        Assert.IsNotNull(player, "The runtime mouse player must compile.");
        MethodInfo method = player.GetMethod("IsInsideCircularNoteTarget",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "The hit check must include pointer position.");
        return (bool)method.Invoke(null, new object[] { point, size });
    }

    private static bool TooEarly(double hitTime, double pressedTime)
    {
        Type player = Type.GetType(PlayerName);
        Assert.IsNotNull(player, "The runtime mouse player must compile.");
        MethodInfo method = player.GetMethod("IsTooEarlyForTap",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "Premature repeated clicks must be consumed.");
        return (bool)method.Invoke(null,
            new object[] { hitTime, pressedTime, 0.13f });
    }

    [Test]
    public void CenterClickIsInsideTheVisibleNoteBody()
    {
        Assert.IsTrue(CircleContains(Vector2.zero, new Vector2(133.4f, 133.4f)));
        Assert.IsTrue(CircleContains(new Vector2(50f, 10f),
            new Vector2(133.4f, 133.4f)));
    }

    [Test]
    public void ClickAnywhereElseMustNotJudgeANote()
    {
        Assert.IsFalse(CircleContains(new Vector2(100f, 0f),
            new Vector2(133.4f, 133.4f)));
        Assert.IsFalse(CircleContains(new Vector2(-100f, 0f),
            new Vector2(133.4f, 133.4f)));
    }

    [Test]
    public void HitAreaIsCircularNotAFreeSquare()
    {
        Assert.IsFalse(CircleContains(new Vector2(60f, 60f),
            new Vector2(133.4f, 133.4f)));
    }

    [Test]
    public void PrematureClickConsumesUpcomingNoteInsteadOfFarmingIt()
    {
        Assert.IsTrue(TooEarly(10d, 9.78d));
        Assert.IsTrue(TooEarly(10d, 9.85d));
        Assert.IsFalse(TooEarly(10d, 9.88d));
        Assert.IsFalse(TooEarly(10d, 9.70d));
    }

    [Test]
    public void NormalAccurateAndLateClicksAreNeverBlockedByAntiSpamGuard()
    {
        Assert.IsFalse(TooEarly(10d, 9.95d));
        Assert.IsFalse(TooEarly(10d, 10d));
        Assert.IsFalse(TooEarly(10d, 10.05d));
        Assert.IsFalse(TooEarly(10d, 10.12d));
    }
}
