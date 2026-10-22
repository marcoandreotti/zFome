namespace ZFome.Infrastructure.Communications;

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "NotificationDelivery";
    public int MaximumAttempts { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 30;
    public int MaximumRetryDelaySeconds { get; set; } = 1800;

    public bool IsValid() => MaximumAttempts is >= 1 and <= 20
        && InitialRetryDelaySeconds is >= 1 and <= 3600
        && MaximumRetryDelaySeconds >= InitialRetryDelaySeconds
        && MaximumRetryDelaySeconds <= 86400;
}

public sealed class NotificationSmtpOptions
{
    public const string SectionName = "NotificationSmtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1025;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
    public int TimeoutSeconds { get; set; } = 15;

    public bool IsValid() => Port is > 0 and <= 65535 && TimeoutSeconds is >= 1 and <= 120;
}

public sealed class MetaWhatsAppOptions
{
    public const string SectionName = "MetaWhatsApp";
    public string GraphApiVersion { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;

    public bool IsValid() => TimeoutSeconds is >= 1 and <= 120;
}
