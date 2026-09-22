using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Devices;

/// <summary>
/// Simülatör cihazlarının kimlik bilgilerini oluşturur (53. gün).
/// </summary>
/// <remarks>
/// <para>
/// 53. günün simülatörü 200 sanal araç çalıştırıyor ve her biri kendi cihaz
/// kimliğiyle token alıyor. O kimlikler veritabanında yoksa simülatör hiçbir
/// şey gönderemez.
/// </para>
/// <para>
/// <b>YALNIZCA GELİŞTİRME ORTAMINDA ÇAĞRILMALI.</b> Sır yapılandırmadan
/// geliyor ve kodda varsayılanı yok — 21. günün tohumlayıcısındaki kuralın
/// aynısı. Sır verilmezse hiçbir şey yapılmıyor; "kolaylık olsun" diye
/// konulan sabit bir sır, üretimde de oradan okunmasıyla biter.
/// </para>
/// <para>
/// Sır <b>hash'lenerek</b> saklanıyor, düz metin değil: veritabanı sızarsa
/// düz metin bir cihaz sırrı, saldırganın o cihaz adına token almasına yeter.
/// </para>
/// </remarks>
public static class DeviceCredentialSeeder
{
    public static async Task SeedSimulatorDevicesAsync(
        ScootlyDbContext dbContext,
        IPasswordHasher<DeviceCredential> hasher,
        string? secret,
        int count,
        string prefix = "sim-",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(hasher);

        if (string.IsNullOrWhiteSpace(secret) || count <= 0)
        {
            return;
        }

        var kimlikler = Enumerable.Range(1, count)
            .Select(i => prefix + i.ToString("D4", CultureInfo.InvariantCulture))
            .ToList();

        // Tek sorguda var olanlar okunuyor. Her kimlik için ayrı bir
        // "var mı" sorgusu, 42. günde avladığımız N+1'in aynısı olurdu.
        var mevcut = await dbContext.DeviceCredentials
            .Where(d => kimlikler.Contains(d.DeviceId))
            .Select(d => d.DeviceId)
            .ToListAsync(cancellationToken);

        var mevcutKume = mevcut.ToHashSet(StringComparer.Ordinal);

        var yeniler = kimlikler
            .Where(id => !mevcutKume.Contains(id))
            .Select(id =>
            {
                var kimlik = new DeviceCredential(id, string.Empty);
                return new DeviceCredential(id, hasher.HashPassword(kimlik, secret));
            })
            .ToList();

        if (yeniler.Count == 0)
        {
            return;
        }

        await dbContext.DeviceCredentials.AddRangeAsync(yeniler, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
