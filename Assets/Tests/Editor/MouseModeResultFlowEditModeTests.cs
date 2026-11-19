using NUnit.Framework;

public sealed class MouseModeResultFlowEditModeTests
{
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
        Assert.AreEqual(expected, MouseModeResultFlow.RankForScore(score));
    }

    [TestCase(0f, "00:00")]
    [TestCase(-2f, "00:00")]
    [TestCase(59.99f, "00:59")]
    [TestCase(125.9f, "02:05")]
    [TestCase(600f, "10:00")]
    public void PlayDurationIsDisplayedAsMinutesAndSeconds(
        float seconds, string expected)
    {
        Assert.AreEqual(expected, MouseModeResultFlow.FormatTime(seconds));
    }
}
