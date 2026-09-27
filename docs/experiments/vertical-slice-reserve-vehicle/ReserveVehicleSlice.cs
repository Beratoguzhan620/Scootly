// DENEYSEL — 78. gün, Vertical Slice karşılaştırması için.
// Gerçek projede kullanılmıyor, yalnızca karşılaştırma amaçlı.

namespace Scootly.Experiments.VerticalSlice;

// --- İstek ve yanıt tipleri ---
public sealed record ReserveVehicleRequest(Guid VehicleId);
public sealed record ReserveVehicleResponse(bool Success, string? Error);

// --- Tek bir sınıfta: doğrulama + iş kuralı + veritabanı erişimi ---
public sealed class ReserveVehicleSlice
{
    private readonly ScootlyDbContext _dbContext; // doğrudan DbContext, repository soyutlaması yok

    public ReserveVehicleSlice(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReserveVehicleResponse> HandleAsync(Guid vehicleId, Guid driverId, CancellationToken ct)
    {
        var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);

        if (vehicle is null)
            return new ReserveVehicleResponse(false, "Araç bulunamadı.");

        if (vehicle.Status != VehicleStatus.Available)
            return new ReserveVehicleResponse(false, "Araç müsait değil.");

        vehicle.Status = VehicleStatus.Reserved; // doğrudan alan erişimi (gerçek Vehicle'da private set korumalı, burada varsayımsal)
        vehicle.ReservedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ReserveVehicleResponse(false, "Başka biri tarafından rezerve edildi.");
        }

        return new ReserveVehicleResponse(true, null);
    }
}

// Bu tek dosyada: istek/yanıt tipleri + doğrulama + iş kuralı + veritabanı erişimi + eşzamanlılık hatası yönetimi hepsi bir arada.