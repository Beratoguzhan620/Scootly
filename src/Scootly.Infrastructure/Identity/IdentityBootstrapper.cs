using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Açılışta, yapılandırılmışsa ilk filo yöneticisi hesabını oluşturur ve rolünü garanti eder.
/// Roller migration ile tohumlanır; bu servis yalnızca "ilk yönetici" tavuk-yumurta sorununu çözer.
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

            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                user = new ApplicationUser { UserName = email, Email = email };
                var created = await userManager.CreateAsync(user, password);

                if (!created.Succeeded)
                {
                    _logger.LogError(
                        "İlk filo yöneticisi oluşturulamadı: {Errors}",
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(user, ScootlyRoles.FleetManager))
                await userManager.AddToRoleAsync(user, ScootlyRoles.FleetManager);

            _logger.LogInformation("İlk filo yöneticisi hesabı hazır.");
        }
        catch (Exception ex)
        {
            // Veritabanı henüz migrate edilmemiş olabilir; uygulamanın açılmasını engellemez.
            _logger.LogError(ex, "İlk filo yöneticisi hesabı hazırlanırken hata oluştu.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}