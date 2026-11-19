using System;
using System.Reflection;
using NUnit.Framework;

public sealed class MouseApproachRingTimingEditModeTests
{
    private static float Scale(double hitTime, double now,
        float lead, float startScale)
    {
        Type type = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer, Assembly-CSharp");
        Assert.IsNotNull(type);
        MethodInfo method = type.GetMethod("ApproachScaleAt",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);
        return (float)method.Invoke(null,
            new object[] { hitTime, now, lead, startScale });
    }

    [Test]
    public void GuideStartsAtConfiguredSize()
    {
        Assert.AreEqual(1.8f, Scale(10d, 8.25d, 1.75f, 1.8f), .0001f);
    }

    [Test]
    public void GuideShrinksContinuouslyTowardHitTime()
    {
        Assert.AreEqual(1.4f, Scale(10d, 9.125d, 1.75f, 1.8f), .0001f);
    }

    [Test]
    public void GuideExactlyMatchesNoteAtJudgementTime()
    {
        Assert.AreEqual(1f, Scale(10d, 10d, 1.75f, 1.8f), .0001f);
    }

    [Test]
    public void GuideDoesNotShrinkPastJudgementSize()
    {
        Assert.AreEqual(1f, Scale(10d, 11d, 1.75f, 1.8f), .0001f);
    }

    [Test]
    public void PreSpawnGuideDoesNotExceedInitialSize()
    {
        Assert.AreEqual(1.8f, Scale(10d, 5d, 1.75f, 1.8f), .0001f);
    }
}
