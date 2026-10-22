namespace ZFome.Infrastructure.Persistence;

public sealed class OutboxProcessingOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 60;
    public int PollingIntervalSeconds { get; set; } = 2;
    public int InitialRetryDelaySeconds { get; set; } = 5;
    public int MaximumRetryDelaySeconds { get; set; } = 3600;
    public int RetentionDays { get; set; } = 30;
    public int CleanupIntervalHours { get; set; } = 12;

    public bool IsValid() =>
        BatchSize is >= 1 and <= 500
        && MaxAttempts is >= 1 and <= 100
        && LeaseDurationSeconds is >= 1 and <= 3600
        && PollingIntervalSeconds is >= 1 and <= 300
        && InitialRetryDelaySeconds is >= 1
        && InitialRetryDelaySeconds is <= 3600
        && MaximumRetryDelaySeconds >= InitialRetryDelaySeconds
        && MaximumRetryDelaySeconds <= 86400
        && RetentionDays is >= 1 and <= 3650
        && CleanupIntervalHours is >= 1 and <= 168;
}
