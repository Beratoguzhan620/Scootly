using Microsoft.Playwright;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;

namespace Scootly.E2E.Tests;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class VehicleEditTests(E2EFixture fixture)
{
    [Fact]
    public async Task Fleet_manager_edits_vehicle_and_sees_the_change_in_the_list()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var oldBrand = $"E2E-{suffix}";
        var newBrand = $"Edited-{suffix}";

        await fixture.Factory.WithDbContextAsync(async db =>
        {
            db.Vehicles.Add(TestData.NewVehicle(brand: oldBrand));
            await db.SaveChangesAsync();
        });

        var manager = await fixture.Factory.CreateUserClientAsync(ScootlyRoles.FleetManager);

        await fixture.RunAsync(nameof(Fleet_manager_edits_vehicle_and_sees_the_change_in_the_list), async page =>
        {
            await page.LoginAsync(manager.Email);

            await page.GotoAsync("/Vehicles");

            var oldRow = page.Locator("tr", new PageLocatorOptions { HasText = oldBrand });
            await oldRow.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "D\u00fczenle" }).ClickAsync();

            await page.GetByLabel("Marka", new PageGetByLabelOptions { Exact = true }).FillAsync(newBrand);
            await page.GetByLabel("Menzil (km)", new PageGetByLabelOptions { Exact = true }).FillAsync("40");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Kaydet" }).ClickAsync();

            var updatedRow = page.Locator("tr", new PageLocatorOptions { HasText = newBrand });
            await Assertions.Expect(updatedRow).ToBeVisibleAsync();
            await Assertions.Expect(updatedRow.Locator("td").Nth(2)).ToHaveTextAsync("40");
            await Assertions.Expect(page.GetByText(oldBrand)).ToHaveCountAsync(0);
        });
    }
}
