using System;
using System.Reflection;
using NUnit.Framework;

public sealed class MouseStabilityRulesEditModeTests
{
    // Gameplay scripts compile into Assembly-CSharp, not this test asmdef.
    private static int Calculate(string judgement, bool realNoteMiss,
        int perfect = 1, int great = 0, int good = 1, int miss = 4)
    {
        Type type = Type.GetType("MouseModeBulletDirector, Assembly-CSharp");
        Assert.IsNotNull(type, "MouseModeBulletDirector did not compile.");

        MethodInfo method = type.GetMethod("JudgementStabilityDelta",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "Missing stability rule calculation.");

        object result = method.Invoke(null,
            new object[] { judgement, realNoteMiss, perfect, great, good, miss });
        return (int)result;
    }

    [Test]
    public void PerfectRestoresStability()
    {
        Assert.AreEqual(1, Calculate("PERFECT", false));
    }

    [Test]
    public void GreatIsNeutralAtThePrototypeDefault()
    {
        Assert.AreEqual(0, Calculate("GREAT", false));
    }

    [Test]
    public void GoodReducesStability()
    {
        Assert.AreEqual(-1, Calculate("GOOD", false));
    }

    [Test]
    public void ActualMissReducesStability()
    {
        Assert.AreEqual(-4, Calculate("MISS", true));
    }

    [Test]
    public void EmptyClickMissDoesNotReduceStabilityAgain()
    {
        Assert.AreEqual(0, Calculate("MISS", false));
    }

    [Test]
    public void InspectorRuleValuesAreRespected()
    {
        Assert.AreEqual(3, Calculate("PERFECT", false, 3, 1, 2, 7));
        Assert.AreEqual(1, Calculate("GREAT", false, 3, 1, 2, 7));
        Assert.AreEqual(-2, Calculate("GOOD", false, 3, 1, 2, 7));
        Assert.AreEqual(-7, Calculate("MISS", true, 3, 1, 2, 7));
    }

    [Test]
    public void InvalidAndNegativeParametersDoNotCauseUnexpectedDamage()
    {
        Assert.AreEqual(0, Calculate("UNKNOWN", true));
        Assert.AreEqual(0, Calculate("GOOD", false, 1, 0, -4, -8));
        Assert.AreEqual(0, Calculate("MISS", true, 1, 0, -4, -8));
    }
}
