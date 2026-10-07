using System.ComponentModel.DataAnnotations;

namespace Scootly.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Bu süreden uzun süren aktif sürüşler terk edilmiş sayılır.</summary>
    [Range(typeof(TimeSpan), "00:15:00", "2.00:00:00")]
    public TimeSpan AbandonedRideThreshold { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Reddedilen bir ödeme en erken bu süre sonra tekrar denenir.</summary>
    [Range(typeof(TimeSpan), "00:00:10", "1.00:00:00")]
    public TimeSpan PaymentRetryBackoff { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Hiç denenmemiş bekleyen ödemeler için tolerans: olay kaybolmuş olsa bile (örn. tüketici kuyruğu henüz yokken yayınlanan mesaj)
    /// sürüş bu süre sonunda ücretlendirilir.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "1.00:00:00")]
    public TimeSpan UnchargedRideGracePeriod { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tamamlanmış bir batarya görevinden sonra, düşük bataryalı aynı araç için uzlaştırma taramasının yeni görev
    /// açmadan önce beklediği süre (cihazın değişen bataryayı bildirmesi için pay).
    /// </summary>
    [Range(typeof(TimeSpan), "00:10:00", "7.00:00:00")]
    public TimeSpan LowBatteryTaskCooldown { get; init; } = TimeSpan.FromHours(6);

    [Range(1, 3650)]
    public int TelemetryRetentionDays { get; init; } = 30;

    /// <summary>ADR 0004: tamamlanmış sürüşlerin konumları bu süreden sonra anonimleştirilir.</summary>
    [Range(1, 3650)]
    public int RideLocationRetentionDays { get; init; } = 90;

    [Range(1, 365)]
    public int OutboxRetentionDays { get; init; } = 7;

    /// <summary>Tekrar teslim penceresinden uzun olmalı; aksi halde eski bir mesaj ikinci kez işlenebilir.</summary>
    [Range(1, 365)]
    public int ProcessedMessageRetentionDays { get; init; } = 30;
}
