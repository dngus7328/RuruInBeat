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
    [NUnit.Framework.Test]
    public void DistantApproachesYieldToImmediateInputAndWheelTail()
    {
        var type = System.Type.GetType("LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer, Assembly-CSharp");
        NUnit.Framework.Assert.IsNotNull(type);
        var cue = type.GetMethod("ApproachVisibilityForOrder");
        var tail = type.GetMethod("WheelTailGuideScale");
        NUnit.Framework.Assert.IsNotNull(cue);
        NUnit.Framework.Assert.IsNotNull(tail);
        float next = (float)cue.Invoke(null, new object[] {1, .5f, true, false});
        float far = (float)cue.Invoke(null, new object[] {6, .5f, false, false});
        float duringWheel = (float)cue.Invoke(null, new object[] {1, .5f, true, true});
        NUnit.Framework.Assert.Greater(next, far * 10f);
        NUnit.Framework.Assert.Less(duringWheel, next);
        NUnit.Framework.Assert.AreEqual(1f, (float)tail.Invoke(null, new object[] {0f}), .001f);
        NUnit.Framework.Assert.AreEqual(1.65f, (float)tail.Invoke(null, new object[] {.55f}), .001f);
    }

}
