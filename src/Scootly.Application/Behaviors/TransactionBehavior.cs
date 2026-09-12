using Scootly.Application.Abstractions;

namespace Scootly.Application.Behaviors;

/// <summary>
/// Bir komutun çalışmasını tek bir işlem (transaction) sınırına alır.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transaction sınırı use case ile örtüşmeli</b> — ne daha geniş, ne daha
/// dar. Controller'da başlatılsaydı sınır HTTP isteğiyle örtüşürdü ve aynı
/// istekte birden fazla iş yapıldığında hepsi tek kilit altında kalırdı;
/// repository'de başlatılsaydı her okuma/yazma ayrı bir işlem olur, aralarında
/// tutarsız bir an doğardı.
/// </para>
/// <para>
/// <b>İşlem içinde dış servis çağrılmaz.</b> Bir ödeme veya cihaz komutu
/// çağrısı transaction'ın içine girerse, veritabanı kilidinin süresi ağ
/// gecikmesine bağlanır: dış servis üç saniye yavaşladığında satır üç saniye
/// kilitli kalır ve o araca dokunmak isteyen herkes bekler. Bu yüzden dış
/// çağrılar işlemin dışına alınır.
/// </para>
/// </remarks>
public sealed class TransactionBehavior
{
    private readonly ITransactionManager _transactions;

    public TransactionBehavior(ITransactionManager transactions)
    {
        _transactions = transactions;
    }

    public async Task<TSonuc> CalistirAsync<TSonuc>(
        Func<CancellationToken, Task<TSonuc>> komut,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(komut);

        await using var islem = await _transactions.BeginTransactionAsync(cancellationToken);

        try
        {
            var sonuc = await komut(cancellationToken);
            await islem.CommitAsync(cancellationToken);
            return sonuc;
        }
        catch
        {
            // Geri alma başarısız olursa asıl istisnanın kaybolmaması için
            // ayrıca yakalanıyor: teşhis için önemli olan, işlemin neden
            // başarısız olduğu; geri almanın da başarısız olması ikincil bilgi.
            try
            {
                await islem.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // İşlem zaten kapanmışsa (bağlantı koptu, veritabanı iptal etti)
                // geri alacak bir şey yok.
            }

            throw;
        }
    }
}
