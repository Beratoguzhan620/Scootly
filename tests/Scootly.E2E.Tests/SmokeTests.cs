using Microsoft.Playwright;

namespace Scootly.E2E.Tests;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class SmokeTests(E2EFixture fixture)
{
    [Fact]
    public async Task Login_page_opens()
    {
        var page = await fixture.NewPageAsync();

        await page.GotoAsync("/Account/Login");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Giri\u015f Yap" }))
            .ToBeVisibleAsync();
    }
}
