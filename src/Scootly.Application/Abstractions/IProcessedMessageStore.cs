namespace Scootly.Application.Abstractions;

/// <summary>Hangi mesajın hangi tüketici tarafından işlendiğini tutar (68. gün).</summary>
/// <remarks>
/// Anahtar (mesaj, tüketici) çifti, yalnızca mesaj değil: aynı olay birden
/// fazla kuyruğa gidebilir ve her tüketicinin onu ayrı ayrı işlemesi gerekir.
/// Yalnızca mesaj kimliği anahtar olsaydı, ilk tüketici işlediğinde ikincisi
/// "zaten işlendi" deyip atlardı.
/// </remarks>
public interface IProcessedMessageStore
{
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default);

    /// <summary>İşlendi kaydını iş birimine ekler; KAYDETMEZ.</summary>
    Task MarkAsProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default);
}
