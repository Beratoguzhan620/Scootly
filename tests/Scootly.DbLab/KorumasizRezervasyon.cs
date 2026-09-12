using System.Data;
using Npgsql;

namespace Scootly.DbLab;

/// <summary>
/// Sürüm damgası eklenmeden ÖNCEKİ davranışı birebir taklit eder:
/// oku → kontrol et → yaz, arada hiçbir koruma yok.
/// </summary>
/// <remarks>
/// Bu kod bilerek ham SQL. Amaç, 38. günde eklenen korumayı geçici olarak
/// kapatmadan "önce" halini gösterebilmek. Gerçek handler'ın 38. gün öncesi
/// hali tam olarak bu üç adımı yapıyordu.
/// </remarks>
internal static class KorumasizRezervasyon
{
    /// <summary>Rezervasyon denemesi. Başarılıysa true.</summary>
    public static async Task<bool> DeneAsync(
        Guid aracId,
        IsolationLevel seviye,
        CancellationToken cancellationToken = default)
    {
        await using var baglanti = await Lab.AcAsync(cancellationToken);
        await using var islem = await baglanti.BeginTransactionAsync(seviye, cancellationToken);

        try
        {
            // 1. OKU
            await using (var oku = new NpgsqlCommand(
                "SELECT \"Status\" FROM \"Vehicles\" WHERE \"Id\" = @id", baglanti, islem))
            {
                oku.Parameters.AddWithValue("id", aracId);
                var durum = (string?)await oku.ExecuteScalarAsync(cancellationToken);

                // 2. KONTROL ET
                if (!string.Equals(durum, "Available", StringComparison.Ordinal))
                {
                    await islem.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            // Okuma ile yazma arasındaki aralık. Gerçek kodda bu aralık
            // mikrosaniyeler; burada genişletiliyor ki yarış penceresi her
            // çalıştırmada güvenilir biçimde açılsın. Hatanın kendisi aynı
            // hata — yalnızca görünür hale getiriliyor.
            await Task.Delay(30, cancellationToken);

            // 3. YAZ
            await using (var yaz = new NpgsqlCommand(
                "UPDATE \"Vehicles\" SET \"Status\" = 'Reserved' WHERE \"Id\" = @id", baglanti, islem))
            {
                yaz.Parameters.AddWithValue("id", aracId);
                await yaz.ExecuteNonQueryAsync(cancellationToken);
            }

            await islem.CommitAsync(cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "40P01")
        {
            // 40001: serileştirme hatası (Serializable seviyesinde beklenir)
            // 40P01: deadlock
            // İkisi de "veritabanı bu işlemi iptal etti" demek; başarısız sayılır.
            await islem.RollbackAsync(CancellationToken.None);
            return false;
        }
    }
}
