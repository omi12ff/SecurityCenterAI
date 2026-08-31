namespace SecurityCenterAI.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "PostgreSQL";

    public string ConnectionString { get; set; } = string.Empty;
}
