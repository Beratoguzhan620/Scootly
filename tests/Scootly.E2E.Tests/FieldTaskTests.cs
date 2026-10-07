using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Scootly.Domain.FieldOps;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;

namespace Scootly.E2E.Tests;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class FieldTaskTests(E2EFixture fixture)
{
    [Fact]
    public async Task Field_operator_claims_an_open_task_and_status_changes()
    {
        var vehicle = TestData.NewVehicle(brand: $"E2E-{Guid.NewGuid().ToString("N")[..6]}");
        var fieldTask = new FieldTask(Guid.NewGuid(), vehicle.Id, FieldTaskType.Inspection, DateTime.UtcNow);

        await fixture.Factory.WithDbContextAsync(async db =>
        {
            db.Vehicles.Add(vehicle);
            db.Set<FieldTask>().Add(fieldTask);
            await db.SaveChangesAsync();
        });

        var fieldOperator = await fixture.Factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);

        await fixture.RunAsync(nameof(Field_operator_claims_an_open_task_and_status_changes), async page =>
        {
            await page.LoginAsync(fieldOperator.Email);

            await page.GotoAsync("/FieldTasks");

            var row = page.Locator("tr", new PageLocatorOptions { HasText = fieldTask.Id.ToString()[..8] });
            await Assertions.Expect(row).ToContainTextAsync("Open");

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "\u00dcstlen" }).ClickAsync();

            await Assertions.Expect(row).ToContainTextAsync("Assigned");
            await Assertions.Expect(row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Tamamla" })).ToBeVisibleAsync();
        });

        var saved = await fixture.Factory.WithDbContextAsync(
            db => db.Set<FieldTask>().AsNoTracking().SingleAsync(t => t.Id == fieldTask.Id));

        Assert.Equal(FieldTaskStatus.Assigned, saved.Status);
        Assert.Equal<Guid?>(fieldOperator.UserId, saved.AssignedTo);
    }
}
