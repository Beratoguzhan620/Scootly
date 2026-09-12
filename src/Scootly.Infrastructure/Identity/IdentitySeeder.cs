using Microsoft.AspNetCore.Identity;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Rolleri ve —yalnızca dışarıdan parola verilmişse— tek bir test kullanıcısını oluşturur.
/// </summary>
/// <remarks>
/// Parola bu dosyada TUTULMAZ. Çağıran taraf onu yapılandırmadan (user-secrets veya
/// ortam değişkeni) okur ve parametre olarak geçer. Sabit bir varsayılan parola
/// yazmak, kodu okuyan herkesin her kurulumdaki hesabın parolasını bilmesi
/// demektir; üstelik böyle bir varsayılan üretime kadar fark edilmeden taşınır.
/// </remarks>
public static class IdentitySeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        string? testUserEmail,
        string? testUserPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new ApplicationRole(roleName));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"'{roleName}' rolü oluşturulamadı: {Describe(roleResult)}");
            }
        }

        // Parola verilmediyse test kullanıcısı hiç oluşturulmaz.
        // Bu sessiz çıkış bilinçli: aksi halde yapılandırmayı unutan bir ortamda
        // tahmin edilebilir parolalı bir hesap kendiliğinden açılırdı.
        if (string.IsNullOrWhiteSpace(testUserEmail) || string.IsNullOrWhiteSpace(testUserPassword))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(testUserEmail) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = testUserEmail,
            Email = testUserEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, testUserPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Test kullanıcısı oluşturulamadı: {Describe(createResult)}");
        }

        var roleAssignment = await userManager.AddToRoleAsync(user, RoleNames.Driver);
        if (!roleAssignment.Succeeded)
        {
            throw new InvalidOperationException(
                $"Test kullanıcısına rol atanamadı: {Describe(roleAssignment)}");
        }
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
