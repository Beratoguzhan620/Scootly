using System.ComponentModel.DataAnnotations;

namespace Scootly.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; init; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required]
    public string UserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public string VirtualHost { get; init; } = "/";
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Kapalıyken outbox yayıncısı ve tüketiciler başlatılmaz (örn. mesaj altyapısı olmayan testler).</summary>
    public bool Enabled { get; init; } = true;

    [Range(1, 1000)]
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>Başarısız bir mesajın yeniden denenmeden önce retry kuyruğunda bekleyeceği süre.</summary>
    [Range(1, 3_600_000)]
    public int RetryDelayMilliseconds { get; init; } = 10_000;

    /// <summary>Bu kadar yeniden denemeden sonra mesaj ölü mektup kuyruğuna (DLQ) taşınır.</summary>
    [Range(0, 100)]
    public int MaxRetryAttempts { get; init; } = 3;

    [Range(1, 1000)]
    public int OutboxBatchSize { get; init; } = 50;

    [Range(100, 600_000)]
    public int OutboxPollIntervalMilliseconds { get; init; } = 2_000;
}
