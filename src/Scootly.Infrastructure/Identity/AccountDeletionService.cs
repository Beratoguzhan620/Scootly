using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// KVKK/GDPR silme hakkı: kullanıcı hesabını siler ve sürüşlerindeki konumları hemen anonimleştirir.
/// Sürüş satırları (ücret, ödeme durumu) muhasebe için kalır; e-posta silindiği için sürücü kimliği artık
/// bir kişiye bağlanamaz. Açık bir yükümlülük (rezervasyon, sürüş, ödenmemiş ücret) varken silme yapılmaz.
/// </summary>
public sealed class AccountDeletionService
{
    private readonly ScootlyDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserSessionValidator _sessionValidator;

    public AccountDeletionService(
        ScootlyDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserSessionValidator sessionValidator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _sessionValidator = sessionValidator;
    }

    public async Task<Result> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return Result.NotFound("Hesap bulunamadı.");

        // Kilitleme sayacı da işler: çalınmış bir token'la parola tahmin edilerek hesap silinemez.
        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (!passwordCheck.Succeeded)
            return Result.Forbidden("Parola doğrulanamadı.");

        if (await _dbContext.Vehicles.AnyAsync(v => v.ReservedBy == userId && v.Status == VehicleStatus.Reserved, cancellationToken))
            return Result.Failure("Aktif bir rezervasyonunuz varken hesabınız silinemez; önce rezervasyonu iptal edin.");

        if (await _dbContext.Rides.AnyAsync(r => r.DriverId == userId && r.Status == RideStatus.Active, cancellationToken))
            return Result.Failure("Devam eden bir sürüşünüz varken hesabınız silinemez.");

        if (await _dbContext.Rides.AnyAsync(
                r => r.DriverId == userId && (r.PaymentStatus == PaymentStatus.Pending || r.PaymentStatus == PaymentStatus.Failed),
                cancellationToken))
        {
            return Result.Failure("Ödenmemiş bir sürüşünüz varken hesabınız silinemez.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Rides"
            SET "StartLatitude" = NULL, "StartLongitude" = NULL, "EndLatitude" = NULL, "EndLongitude" = NULL
            WHERE "DriverId" = {userId}
            """, cancellationToken);

        var deleted = await _userManager.DeleteAsync(user);

        if (!deleted.Succeeded)
            throw new InvalidOperationException($"Hesap silinemedi: {string.Join("; ", deleted.Errors.Select(e => e.Code))}");

        await transaction.CommitAsync(cancellationToken);

        _sessionValidator.Invalidate(userId);

        return Result.Success();
    }
}
