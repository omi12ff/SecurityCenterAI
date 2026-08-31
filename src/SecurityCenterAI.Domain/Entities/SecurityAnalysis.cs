namespace SecurityCenterAI.Domain.Entities;

public sealed class SecurityAnalysis
{
    private SecurityAnalysis()
    {
    }

    public SecurityAnalysis(
        Guid userId,
        int score,
        DateTime? analyzedAtUtc = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador del usuario no puede estar vacío.",
                nameof(userId));
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                "El puntaje debe estar entre 0 y 100.");
        }

        var timestamp = analyzedAtUtc ?? DateTime.UtcNow;
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "La fecha del análisis debe expresarse en UTC.",
                nameof(analyzedAtUtc));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Score = score;
        AnalyzedAtUtc = timestamp;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int Score { get; private set; }
    public DateTime AnalyzedAtUtc { get; private set; }
    public User User { get; private set; } = null!;
}
