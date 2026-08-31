using SecurityCenterAI.Domain.Entities;

namespace SecurityCenterAI.Tests;

public sealed class SecurityAnalysisTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_AcceptsScoreBoundaries(int score)
    {
        var analysis = new SecurityAnalysis(
            Guid.NewGuid(),
            score,
            DateTime.UtcNow);

        Assert.Equal(score, analysis.Score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_RejectsScoresOutsideTheRange(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SecurityAnalysis(Guid.NewGuid(), score, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsMissingUserAndNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() =>
            new SecurityAnalysis(Guid.Empty, 50, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() =>
            new SecurityAnalysis(Guid.NewGuid(), 50, DateTime.Now));
    }
}
