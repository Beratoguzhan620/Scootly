using Microsoft.Playwright;
using Scootly.Testing;

namespace Scootly.E2E.Tests;

internal static class PageExtensions
{
    /// <summary>Giris formunu doldurur ve oturumun acildigini ("Cikis Yap" butonu) dogrular.</summary>
    public static async Task LoginAsync(this IPage page, string email)
    {
        await page.GotoAsync("/Account/Login");

        await page.GetByLabel("E-posta", new PageGetByLabelOptions { Exact = true }).FillAsync(email);
        await page.GetByLabel("Parola", new PageGetByLabelOptions { Exact = true }).FillAsync(TestSecrets.DefaultPassword);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Giri\u015f Yap" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "\u00c7\u0131k\u0131\u015f Yap" }))
            .ToBeVisibleAsync();
    }
}