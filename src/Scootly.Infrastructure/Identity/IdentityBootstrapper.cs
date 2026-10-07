using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Açılışta, yapılandırılmışsa ilk filo yöneticisi hesabını oluşturur. Roller migration ile tohumlanır; bu servis
/// yalnızca "ilk yönetici" tavuk-yumurta sorununu çözer.
/// <list type="bullet">
/// <item>Hesap ve rolü tek transaction'da yazılır: yarım (rolsüz) hesap kalmaz.</item>
/// <item>Bu e-postayla bir hesap zaten varsa ona yetki VERİLMEZ. Kayıt herkese açık ve e-posta doğrulanmadığı için
/// "önce aynı e-postayla kaydol, açılışta yönetici ol" yoluyla yetki yükseltmesi böylece kapanır; yöneticinin
/// bilerek kaldırdığı rol de her açılışta geri gelmez.</item>
/// </list>
/// </summary>
public sealed class IdentityBootstrapper : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IOptions<BootstrapOptions> _options;
    private readonly ILogger<IdentityBootstrapper> _logger;

    public IdentityBootstrapper(IServiceProvider services, IOptions<BootstrapOptions> options, ILogger<IdentityBootstrapper> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _options.Value.FleetManagerEmail;
        var password = _options.Value.FleetManagerPassword;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        try
        {
            using var scope = _services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var existing = await userManager.FindByEmailAsync(email);

            if (existing is not null)
            {
                if (!await userManager.IsInRoleAsync(existing, ScootlyRoles.FleetManager))
                {
                    _logger.LogWarning(
                        "Bootstrap e-postası filo yöneticisi olmayan mevcut bir hesaba ait; yetki verilmedi. " +
                        "Rolü bilinçli olarak vermek için yönetim ucunu kullanın: {UserId}", existing.Id);
                }

                return;
            }

            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, password);

            if (!created.Succeeded)
            {
                // Birden fazla Api kopyası aynı anda açılırsa biri hesabı önce oluşturur; bu bir hata değil.
                if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
                {
                    _logger.LogInformation("İlk filo yöneticisi hesabı başka bir kopya tarafından oluşturuldu.");
                    return;
                }

                _logger.LogError("İlk filo yöneticisi oluşturulamadı: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            var roleAdded = await userManager.AddToRoleAsync(user, ScootlyRoles.FleetManager);

            if (!roleAdded.Succeeded)
            {
                _logger.LogError("İlk filo yöneticisine rol atanamadı: {Errors}", string.Join("; ", roleAdded.Errors.Select(e => e.Code)));
                return;
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("İlk filo yöneticisi hesabı oluşturuldu: {UserId}", user.Id);
        }
        catch (UniqueConstraintViolationException)
        {
            // Aynı anda açılan iki kopya doğrulamayı birlikte geçip aynı satırı yazmaya çalıştı; diğeri kazandı.
            _logger.LogInformation("İlk filo yöneticisi hesabı başka bir kopya tarafından oluşturuldu.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Veritabanı henüz migrate edilmemiş olabilir; uygulamanın açılmasını engellemez.
            _logger.LogError(ex, "İlk filo yöneticisi hesabı hazırlanırken hata oluştu.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
