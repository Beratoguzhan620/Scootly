using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Behaviors;

/// <summary>Bir mesajın işlenmesinin sonucu.</summary>
/// <param name="Result">İşleyicinin sonucu (tekrar mesajda başarı).</param>
/// <param name="WasDuplicate">Mesaj daha önce işlenmişti ve atlandı.</param>
public sealed record IdempotentOutcome(Result Result, bool WasDuplicate);

/// <summary>
/// Aynı mesajın iki kez işlenmesini engeller (68. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Neden gerekli:</b> kuyruk "en az bir kez" teslim ediyor. Tüketici işi
/// yapıp onay (ack) gönderemeden çökerse mesaj tekrar gelir. Kontrol olmadan
/// bunun sonucu aynı sürüş için iki ücret, iki ödeme, iki borç.
/// </para>
/// <para>
/// <b>Asıl püf noktası atomiklik.</b> "İşlendi" kaydı, işin kendisiyle AYNI
/// transaction'da yazılıyor: kayıt önce iş birimine ekleniyor, işleyici
/// <c>SaveChangesAsync</c> çağırdığında ikisi birlikte gidiyor. Ayrı yazılsaydı
/// iki kötü sıra vardı — önce kayıt sonra iş: arada çökmek işi hiç yapılmamış
/// ama "yapıldı" diye işaretli bırakır; önce iş sonra kayıt: arada çökmek işi
/// iki kez yaptırır.
/// </para>
/// <para>
/// <b>Aynı anda iki tüketici:</b> ikisi de "işlenmedi" görebilir. O durumda
/// ikincisinin kaydı birincil anahtar ihlaliyle reddedilir, onunla birlikte
/// işin kendisi de geri alınır (aynı transaction), mesaj geçici hata olarak
/// yeniden denenir ve bu sefer "işlendi" yolundan çıkar.
/// </para>
/// <para>
/// ADR 0010 "her işleyici kendi kontrolünü açıkça yazsın" diye ön karar
/// vermişti; itirazı öznitelik + yansıma sihrineydi. Burada sihir yok: bu sınıf
/// tek bir yerden (Worker'ın tüketici servisi ve webhook ucu) açıkça
/// çağrılıyor. Kontrolü her işleyiciye yazmak, yeni bir tüketici eklendiğinde
/// unutulabilecek bir satır demekti. Bkz. ADR 0015.
/// </para>
/// </remarks>
public sealed class IdempotencyBehavior
{
    private readonly IProcessedMessageStore _store;
    private readonly IUnitOfWork _unitOfWork;

    public IdempotencyBehavior(IProcessedMessageStore store, IUnitOfWork unitOfWork)
    {
        _store = store;
        _unitOfWork = unitOfWork;
    }

    public async Task<IdempotentOutcome> ExecuteAsync(
        Guid messageId,
        string consumer,
        Func<CancellationToken, Task<Result>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        if (messageId == Guid.Empty)
            return new IdempotentOutcome(Result.Failure("Mesaj kimligi bos; tekrar kontrolu yapilamaz."), false);

        if (await _store.HasBeenProcessedAsync(messageId, consumer, cancellationToken))
            return new IdempotentOutcome(Result.Success(), true);

        await _store.MarkAsProcessedAsync(messageId, consumer, cancellationToken);

        var sonuc = await handler(cancellationToken);

        if (!sonuc.IsSuccess)
            return new IdempotentOutcome(sonuc, false);

        // Isleyici zaten kaydettiyse bu cagri bos gecer. Kaydetmediyse
        // (ornegin "is zaten yapilmis" deyip hicbir sey degistirmediyse)
        // yalnizca "islendi" kaydi yazilir.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdempotentOutcome(sonuc, false);
    }
}
